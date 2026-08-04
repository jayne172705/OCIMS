using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using eSureHi.Data;
using eSureHi.Services;
using eSureHi.Views.Shared;
using MySqlConnector;
using QuestPDF.Infrastructure;

namespace eSureHi
{
    public partial class App : Application
    {
        public static DatabaseConfiguration DbConfig { get; set; } = new();
        public static Window? ActiveShell { get; set; }

        public App()
        {
            // ── Hook exception handlers early in constructor ──────
            AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
            Dispatcher.UnhandledException += Dispatcher_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private static void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogError("UNHANDLED EXCEPTION", e.ExceptionObject as Exception);
        }

        private static void Dispatcher_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ReportError("DISPATCHER EXCEPTION", e.Exception, showMessage: false);
            e.Handled = true;
        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogError("TASK EXCEPTION", e.Exception);
            e.SetObserved();
        }

        public static void ReportError(string title, Exception? ex, bool showMessage = true)
        {
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}\n{ex}");
            if (showMessage)
                MessageBox.Show($"{title}\n\n{ex?.Message}", "eSureHi Error", MessageBoxButton.OK, MessageBoxImage.Error);       
        }
        private static void LogError(string title, Exception? ex) => ReportError(title, ex);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // QuestPDF community license
                try { QuestPDF.Settings.License = LicenseType.Community; }
                catch (Exception ex) { LogError("QuestPDF License", ex); }

                // Load configuration
                try { DbConfig = DatabaseConfiguration.Load(); }
                catch (Exception ex) { LogError("Config Load", ex); }

                // Initialize local SQLite database in background.
                _ = InitializeDatabaseAsync();

                // Create and show login window in try-catch
                try
                {
                    var loginWindow = new LoginWindow();
                    loginWindow.Show();
                }
                catch (Exception ex)
                {
                    LogError("LoginWindow Failed", ex);
                    // If login window fails, show a simple window as fallback
                    var testWindow = new Window
                    {
                        Title = "eSureHi - Error",
                        Width = 400,
                        Height = 200,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };
                    var tb = new System.Windows.Controls.TextBlock
                    {
                        Text = $"Failed to load login window:\n\n{ex.Message}\n\n{ex.InnerException?.Message}",
                        Margin = new System.Windows.Thickness(20),
                        TextWrapping = System.Windows.TextWrapping.Wrap,
                        Foreground = System.Windows.Media.Brushes.Red
                    };
                    testWindow.Content = tb;
                    testWindow.Show();
                }
            }
            catch (Exception ex)
            {
                LogError("OnStartup Fatal", ex);
                try
                {
                    var w = new Window 
                    { 
                        Title = "Fatal Error", 
                        Width = 500, 
                        Height = 300,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };
                    w.Content = new System.Windows.Controls.TextBlock 
                    { 
                        Text = $"{ex}", 
                        Margin = new System.Windows.Thickness(20),
                        TextWrapping = System.Windows.TextWrapping.Wrap
                    };
                    w.Show();
                }
                catch { }
            }
        }

        private static async System.Threading.Tasks.Task InitializeDatabaseAsync()
        {
            try
            {
                await LocalDatabaseInitializer.InitializeAsync();
                await AuthService.Instance.SeedAdminAsync();
                await AuthService.Instance.SeedWebFlowAccountsAsync();
                await AuthService.Instance.SeedDefaultEmployeeAsync();
                await AuthService.Instance.SeedDocumentTypesAsync();

                var canConnect = await eSureHiDbContextFactory.CanConnectAsync();
                if (!canConnect)
                {
                    await OfflineOnlineSyncService.CheckStatusAsync();
                    OfflineOnlineSyncService.StartBackgroundSync();
                    return;
                }

                await EnsureBeneficiaryColumnsAsync();
                await EnsureBeneficiarySourceOfFundsColumnAsync();
                await EnsurePolicyTrackSchemaAsync();
                await EnsureSourceFundsTableAsync();
                await EnsureResidentDemographicsTableAsync();
                await AuthService.EnsureCloudUserPermissionsTableAsync();
                await EnsureOptionalColumnAsync("employees", "barangay", "LONGTEXT NULL");
                await EnsureOptionalColumnAsync("beneficiaries", "email", "VARCHAR(160) NULL");
                await EnsureOptionalColumnAsync("system_users", "ben_id", "INT NULL");
                await EnsureOptionalColumnAsync("employee_policies", "status_remarks", "LONGTEXT NULL");
                await EnsureOptionalColumnAsync("vw_employee_coverage", "barangay", "LONGTEXT NULL");
                await EnsureOptionalColumnAsync("vw_claims_summary", "barangay", "LONGTEXT NULL");
                await EnsureNullableDateColumnAsync("employees", "date_of_birth");
                await EnsureNullableDateColumnAsync("beneficiaries", "date_of_birth");
                await EnsureSourceOfFundsColumnAsync("claims");
                await EnsureOptionalColumnAsync("claims", "admission_days", "INT NOT NULL DEFAULT 0");
                await EnsureOptionalColumnAsync("claims", "covered_allowance_days", "INT NOT NULL DEFAULT 0");
                await EnsureOptionalColumnAsync("claims", "daily_allowance_rate", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                await EnsureOptionalColumnAsync("claims", "daily_allowance_amount", "DECIMAL(18,2) NOT NULL DEFAULT 0");
                await EnsureSourceOfFundsColumnAsync("benefits");

                await OfflineOnlineSyncService.SyncAsync(SyncDirection.TwoWay);
                OfflineOnlineSyncService.StartBackgroundSync();
            }
            catch (Exception ex)
            {
                LogError("Database Initialization", ex);
                OfflineOnlineSyncService.StartBackgroundSync();
            }
        }

        private static async System.Threading.Tasks.Task EnsureBeneficiaryColumnsAsync()
        {
            await EnsureOptionalColumnAsync("beneficiaries", "beneficiary_id", "LONGTEXT NULL");
            await EnsureOptionalColumnAsync("beneficiaries", "civil_registry_id", "LONGTEXT NULL");
            await EnsureOptionalColumnAsync("beneficiaries", "recipients_insurance", "LONGTEXT NULL");
            await EnsureOptionalColumnAsync("beneficiaries", "cedula_no", "LONGTEXT NULL");
            await EnsureOptionalColumnAsync("beneficiaries", "received", "TINYINT(1) NOT NULL DEFAULT 0");
            await EnsureOptionalColumnAsync("beneficiaries", "contribution", "DECIMAL(18,2) NOT NULL DEFAULT 0");
            await EnsureOptionalColumnAsync("beneficiaries", "source_of_funds", "LONGTEXT NULL");
            await EnsureOptionalColumnAsync("beneficiaries", "workflow_status", "VARCHAR(40) NOT NULL DEFAULT 'Pending'");
            await EnsureOptionalColumnAsync("beneficiaries", "status_remarks", "LONGTEXT NULL");
            await EnsureOptionalColumnAsync("beneficiaries", "is_admin_confirmed", "TINYINT(1) NOT NULL DEFAULT 1");
        }

        private static async System.Threading.Tasks.Task EnsureBeneficiarySourceOfFundsColumnAsync()
        {
            await using var conn = new MySqlConnection(DbConfig.ToConnectionString());
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'beneficiaries'
                  AND COLUMN_NAME = 'source_of_funds';";

            var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
            if (exists)
                return;

            await using var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE beneficiaries ADD COLUMN source_of_funds LONGTEXT NULL;";
            await alterCmd.ExecuteNonQueryAsync();
        }

        private static async System.Threading.Tasks.Task EnsureSourceFundsTableAsync()
        {
            await using var conn = new MySqlConnection(DbConfig.ToConnectionString());
            await conn.OpenAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS source_funds (
                        source_fund_id INT NOT NULL AUTO_INCREMENT,
                        fund_name VARCHAR(120) NOT NULL,
                        fund_type VARCHAR(40) NOT NULL DEFAULT 'LGU',
                        description VARCHAR(500) NULL,
                        allocated_amount DECIMAL(18,2) NOT NULL DEFAULT 0,
                        used_amount DECIMAL(18,2) NOT NULL DEFAULT 0,
                        status VARCHAR(30) NOT NULL DEFAULT 'Active',
                        ggms_office_code VARCHAR(80) NULL,
                        created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                        PRIMARY KEY (source_fund_id),
                        UNIQUE KEY ux_source_funds_name (fund_name)
                    );";
                await cmd.ExecuteNonQueryAsync();
            }

            await SeedSourceFundAsync(conn, "Job Order", "Employee Track", 250000m,
                "Budget allocation for Job Order employee applicants and assignments.",
                "OFF-2026-0004");
            await SeedSourceFundAsync(conn, "Casual", "Employee Track", 500000m,
                "Budget allocation for Casual employee applicants and assignments.",
                null);
            await SeedSourceFundAsync(conn, "Regular", "Employee Track", 1000000m,
                "Budget allocation for Regular employee applicants and assignments.",
                "OFF-2026-0004");
        }

        private static async System.Threading.Tasks.Task EnsureResidentDemographicsTableAsync()
        {
            await using var conn = new MySqlConnection(DbConfig.ToConnectionString());
            await conn.OpenAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS resident_demographics (
                        resident_demographic_id BIGINT NOT NULL AUTO_INCREMENT,
                        residents_id BIGINT NULL,
                        beneficiary_id VARCHAR(80) NULL,
                        civilregistry_id VARCHAR(80) NULL,
                        family_id VARCHAR(80) NULL,
                        household_id VARCHAR(80) NULL,
                        family_role VARCHAR(80) NULL,
                        relationship_to_head VARCHAR(80) NULL,
                        is_household_head TINYINT(1) NOT NULL DEFAULT 0,
                        source VARCHAR(80) NOT NULL DEFAULT 'Manual',
                        remarks VARCHAR(500) NULL,
                        created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                        `SyncId` CHAR(36) NOT NULL,
                        PRIMARY KEY (resident_demographic_id),
                        UNIQUE KEY ux_resident_demographics_syncid (`SyncId`),
                        KEY ix_resident_demographics_residents_id (residents_id),
                        KEY ix_resident_demographics_beneficiary_id (beneficiary_id),
                        KEY ix_resident_demographics_civilregistry_id (civilregistry_id)
                    );";
                await cmd.ExecuteNonQueryAsync();
            }

            await EnsureOptionalColumnAsync("resident_demographics", "family_id", "VARCHAR(80) NULL");
            await EnsureOptionalColumnAsync("resident_demographics", "household_id", "VARCHAR(80) NULL");
            await EnsureOptionalColumnAsync("resident_demographics", "relationship_to_head", "VARCHAR(80) NULL");
            await EnsureOptionalColumnAsync("resident_demographics", "is_household_head", "TINYINT(1) NOT NULL DEFAULT 0");
            await EnsureOptionalColumnAsync("resident_demographics", "source", "VARCHAR(80) NOT NULL DEFAULT 'Manual'");
            await EnsureOptionalColumnAsync("resident_demographics", "remarks", "VARCHAR(500) NULL");
            await EnsureOptionalColumnAsync("resident_demographics", "created_at", "DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP");
            await EnsureOptionalColumnAsync("resident_demographics", "updated_at", "DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP");
            await EnsureOptionalColumnAsync("resident_demographics", "SyncId", "CHAR(36) NULL");
        }

        private static async System.Threading.Tasks.Task EnsurePolicyTrackSchemaAsync()
        {
            await using var conn = new MySqlConnection(DbConfig.ToConnectionString());
            await conn.OpenAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    ALTER TABLE insurance_policies
                    MODIFY policy_type ENUM(
                        'Job Order','Casual','Regular',
                        'Health','Life','Accident','Disability','Retirement','Dental','Vision','Savings'
                    ) NOT NULL;";
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    ALTER TABLE employee_policies
                    MODIFY assignment_status ENUM(
                        'Active','Inactive','Pending','Expired','Terminated','On-Hold'
                    ) DEFAULT 'Active';";
                await cmd.ExecuteNonQueryAsync();
            }

            var hasSyncId = await ColumnExistsAsync(conn, "insurance_policies", "SyncId");
            await SeedTrackPolicyAsync(conn, "POL-JOBORDER-001", "Job Order", 250000m, hasSyncId);
            await SeedTrackPolicyAsync(conn, "POL-CASUAL-001", "Casual", 500000m, hasSyncId);
            await SeedTrackPolicyAsync(conn, "POL-REGULAR-001", "Regular", 1000000m, hasSyncId);
        }

        private static async System.Threading.Tasks.Task SeedTrackPolicyAsync(
            MySqlConnection conn,
            string policyCode,
            string trackName,
            decimal coverageAmount,
            bool hasSyncId)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = hasSyncId
                ? @"
                UPDATE insurance_policies
                SET policy_name = @track_name,
                    policy_type = @track_name,
                    provider_name = 'LGU Sulop HRMO / Budget Office',
                    coverage_amount = @coverage_amount,
                    description = @description,
                    terms_conditions = 'Auto-assigned based on employee type.',
                    policy_status = 'Active',
                    updated_at = NOW(),
                    `SyncId` = CASE
                        WHEN `SyncId` IS NULL OR `SyncId` = '' THEN UUID()
                        ELSE `SyncId`
                    END
                WHERE policy_code = @policy_code;

                INSERT INTO insurance_policies
                    (policy_code, policy_name, policy_type, provider_name,
                     effective_date, coverage_amount, description,
                     terms_conditions, policy_status, created_at, updated_at, `SyncId`)
                SELECT
                    @policy_code, @track_name, @track_name,
                    'LGU Sulop HRMO / Budget Office',
                    CURDATE(), @coverage_amount,
                    @description,
                    'Auto-assigned based on employee type.',
                    'Active', NOW(), NOW(), UUID()
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM insurance_policies
                    WHERE policy_code = @policy_code
                );"
                : @"
                UPDATE insurance_policies
                SET policy_name = @track_name,
                    policy_type = @track_name,
                    provider_name = 'LGU Sulop HRMO / Budget Office',
                    coverage_amount = @coverage_amount,
                    description = @description,
                    terms_conditions = 'Auto-assigned based on employee type.',
                    policy_status = 'Active',
                    updated_at = NOW()
                WHERE policy_code = @policy_code;

                INSERT INTO insurance_policies
                    (policy_code, policy_name, policy_type, provider_name,
                     effective_date, coverage_amount, description,
                     terms_conditions, policy_status, created_at, updated_at)
                SELECT
                    @policy_code, @track_name, @track_name,
                    'LGU Sulop HRMO / Budget Office',
                    CURDATE(), @coverage_amount,
                    @description,
                    'Auto-assigned based on employee type.',
                    'Active', NOW(), NOW()
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM insurance_policies
                    WHERE policy_code = @policy_code
                );";
            cmd.Parameters.AddWithValue("@policy_code", policyCode);
            cmd.Parameters.AddWithValue("@track_name", trackName);
            cmd.Parameters.AddWithValue("@coverage_amount", coverageAmount);
            cmd.Parameters.AddWithValue("@description",
                $"Insurance track for {trackName} employees.");
            await cmd.ExecuteNonQueryAsync();
        }

        private static async System.Threading.Tasks.Task<bool> ColumnExistsAsync(
            MySqlConnection conn,
            string tableName,
            string columnName)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @table_name
                  AND COLUMN_NAME = @column_name;";
            cmd.Parameters.AddWithValue("@table_name", tableName);
            cmd.Parameters.AddWithValue("@column_name", columnName);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        private static async System.Threading.Tasks.Task EnsureSourceOfFundsColumnAsync(string tableName)
        {
            await using var conn = new MySqlConnection(DbConfig.ToConnectionString());
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @table_name
                  AND COLUMN_NAME = 'source_of_funds';";
            cmd.Parameters.AddWithValue("@table_name", tableName);

            var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
            if (exists)
                return;

            await using var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = $"ALTER TABLE `{tableName}` ADD COLUMN source_of_funds VARCHAR(120) NULL;";
            await alterCmd.ExecuteNonQueryAsync();
        }

        private static async System.Threading.Tasks.Task EnsureOptionalColumnAsync(
            string tableName,
            string columnName,
            string columnDefinition)
        {
            try
            {
                await using var conn = new MySqlConnection(DbConfig.ToConnectionString());
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = @table_name
                      AND COLUMN_NAME = @column_name;";
                cmd.Parameters.AddWithValue("@table_name", tableName);
                cmd.Parameters.AddWithValue("@column_name", columnName);

                var exists = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                if (exists)
                    return;

                await using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = $"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {columnDefinition};";
                await alterCmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Reports have a fallback SELECT for older online schemas or real SQL views.
            }
        }

        private static async System.Threading.Tasks.Task EnsureNullableDateColumnAsync(
            string tableName,
            string columnName)
        {
            try
            {
                await using var conn = new MySqlConnection(DbConfig.ToConnectionString());
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT IS_NULLABLE
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = @table_name
                      AND COLUMN_NAME = @column_name
                    LIMIT 1;";
                cmd.Parameters.AddWithValue("@table_name", tableName);
                cmd.Parameters.AddWithValue("@column_name", columnName);

                var isNullable = (await cmd.ExecuteScalarAsync())?.ToString();
                if (string.Equals(isNullable, "YES", StringComparison.OrdinalIgnoreCase))
                    return;

                await using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = $"ALTER TABLE `{tableName}` MODIFY COLUMN `{columnName}` DATE NULL;";
                await alterCmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Some older remote schemas may not allow ALTERs; runtime fallbacks still keep the app usable.
            }
        }

        private static async System.Threading.Tasks.Task SeedSourceFundAsync(
            MySqlConnection conn,
            string fundName,
            string fundType,
            decimal allocatedAmount,
            string description,
            string? ggmsOfficeCode)
        {
            await using var cmd = conn.CreateCommand();
            var hasSyncId = await ColumnExistsAsync(conn, "source_funds", "SyncId");
            cmd.CommandText = hasSyncId
                ? @"
                INSERT INTO source_funds
                    (fund_name, fund_type, allocated_amount, description, status, ggms_office_code, `SyncId`)
                VALUES
                    (@fund_name, @fund_type, @allocated_amount, @description, 'Active', @ggms_office_code, UUID())
                ON DUPLICATE KEY UPDATE
                    fund_type = VALUES(fund_type),
                    allocated_amount = CASE
                        WHEN allocated_amount IS NULL OR allocated_amount <= 0 THEN VALUES(allocated_amount)
                        ELSE allocated_amount
                    END,
                    description = VALUES(description),
                    status = 'Active',
                    ggms_office_code = VALUES(ggms_office_code),
                    `SyncId` = CASE
                        WHEN `SyncId` IS NULL OR `SyncId` = '' THEN VALUES(`SyncId`)
                        ELSE `SyncId`
                    END;"
                : @"
                INSERT INTO source_funds
                    (fund_name, fund_type, allocated_amount, description, status, ggms_office_code)
                VALUES
                    (@fund_name, @fund_type, @allocated_amount, @description, 'Active', @ggms_office_code)
                ON DUPLICATE KEY UPDATE
                    fund_type = VALUES(fund_type),
                    allocated_amount = CASE
                        WHEN allocated_amount IS NULL OR allocated_amount <= 0 THEN VALUES(allocated_amount)
                        ELSE allocated_amount
                    END,
                    description = VALUES(description),
                    status = 'Active',
                    ggms_office_code = VALUES(ggms_office_code);";
            cmd.Parameters.AddWithValue("@fund_name", fundName);
            cmd.Parameters.AddWithValue("@fund_type", fundType);
            cmd.Parameters.AddWithValue("@allocated_amount", allocatedAmount);
            cmd.Parameters.AddWithValue("@description", description);
            cmd.Parameters.AddWithValue("@ggms_office_code", (object?)ggmsOfficeCode ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
