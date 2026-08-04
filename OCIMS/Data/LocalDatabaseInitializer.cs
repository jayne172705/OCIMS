using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace eSureHi.Data
{
    public static class LocalDatabaseInitializer
    {
        public static async Task InitializeAsync()
        {
            await using var db = eSureHiDbContextFactory.Create();
            await db.Database.EnsureCreatedAsync();
            await EnsureResidentDemographicsTableAsync(db);
            await EnsureCrsBeneficiaryCacheTableAsync(db);
            await EnsureDistributionTablesAsync(db);
            await EnsureClaimColumnsAsync(db);
            await EnsurePaymentsTableAsync(db);
            await EnsureSyncColumnsAsync(db);
            await EnsureLocalViewsAsync(db);
            await EnsureBeneficiaryStagingIndexesAsync(db);
            await EnsureBeneficiaryConfirmationColumnAsync(db);
            await BackfillSyncIdsAsync(db);
        }

        private static async Task EnsureBeneficiaryStagingIndexesAsync(eSureHiDbContext db)
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IF NOT EXISTS ix_beneficiary_staging_beneficiary_id
                ON beneficiary_staging (beneficiary_id);");
        }

        private static async Task EnsureBeneficiaryConfirmationColumnAsync(eSureHiDbContext db)
        {
            await EnsureSqliteColumnAsync(db, "beneficiaries", "is_admin_confirmed", "INTEGER NOT NULL DEFAULT 1");
        }

        private static async Task EnsureResidentDemographicsTableAsync(eSureHiDbContext db)
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS resident_demographics (
                    resident_demographic_id INTEGER NOT NULL CONSTRAINT PK_resident_demographics PRIMARY KEY AUTOINCREMENT,
                    residents_id INTEGER NULL,
                    beneficiary_id TEXT NULL,
                    civilregistry_id TEXT NULL,
                    family_id TEXT NULL,
                    household_id TEXT NULL,
                    family_role TEXT NULL,
                    relationship_to_head TEXT NULL,
                    is_household_head INTEGER NOT NULL DEFAULT 0,
                    source TEXT NOT NULL DEFAULT 'Manual',
                    remarks TEXT NULL,
                    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );");

            await EnsureSqliteColumnAsync(db, "resident_demographics", "residents_id", "INTEGER NULL");
            await EnsureSqliteColumnAsync(db, "resident_demographics", "beneficiary_id", "TEXT NULL");
            await EnsureSqliteColumnAsync(db, "resident_demographics", "civilregistry_id", "TEXT NULL");
            await EnsureSqliteColumnAsync(db, "resident_demographics", "family_id", "TEXT NULL");
            await EnsureSqliteColumnAsync(db, "resident_demographics", "household_id", "TEXT NULL");
            await EnsureSqliteColumnAsync(db, "resident_demographics", "family_role", "TEXT NULL");
            await EnsureSqliteColumnAsync(db, "resident_demographics", "relationship_to_head", "TEXT NULL");
            await EnsureSqliteColumnAsync(db, "resident_demographics", "is_household_head", "INTEGER NOT NULL DEFAULT 0");
            await EnsureSqliteColumnAsync(db, "resident_demographics", "source", "TEXT NOT NULL DEFAULT 'Manual'");
            await EnsureSqliteColumnAsync(db, "resident_demographics", "remarks", "TEXT NULL");
            await EnsureSqliteColumnAsync(db, "resident_demographics", "created_at", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP");
            await EnsureSqliteColumnAsync(db, "resident_demographics", "updated_at", "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IF NOT EXISTS ix_resident_demographics_residents_id
                ON resident_demographics (residents_id);");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IF NOT EXISTS ix_resident_demographics_beneficiary_id
                ON resident_demographics (beneficiary_id);");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IF NOT EXISTS ix_resident_demographics_civilregistry_id
                ON resident_demographics (civilregistry_id);");
        }

        private static async Task EnsureCrsBeneficiaryCacheTableAsync(eSureHiDbContext db)
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS crs_beneficiary_cache (
                    beneficiary_cache_id INTEGER NOT NULL CONSTRAINT PK_crs_beneficiary_cache PRIMARY KEY AUTOINCREMENT,
                    beneficiary_id TEXT NOT NULL,
                    residents_id INTEGER NULL,
                    civilregistry_id TEXT NULL,
                    full_name TEXT NULL,
                    first_name TEXT NULL,
                    last_name TEXT NULL,
                    middle_name TEXT NULL,
                    sex TEXT NULL,
                    age TEXT NULL,
                    address TEXT NULL,
                    date_of_birth TEXT NULL,
                    marital_status TEXT NULL,
                    is_pwd INTEGER NOT NULL DEFAULT 0,
                    is_senior INTEGER NOT NULL DEFAULT 0,
                    family_id TEXT NULL,
                    household_id TEXT NULL,
                    family_role TEXT NULL,
                    relationship_to_head TEXT NULL,
                    is_household_head INTEGER NOT NULL DEFAULT 0,
                    cached_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );");

            await EnsureSqliteColumnAsync(db, "crs_beneficiary_cache", "residents_id", "INTEGER NULL");
            await EnsureSqliteColumnAsync(db, "crs_beneficiary_cache", "civilregistry_id", "TEXT NULL");
            await EnsureSqliteColumnAsync(db, "crs_beneficiary_cache", "family_id", "TEXT NULL");
            await EnsureSqliteColumnAsync(db, "crs_beneficiary_cache", "household_id", "TEXT NULL");
            await EnsureSqliteColumnAsync(db, "crs_beneficiary_cache", "family_role", "TEXT NULL");
            await EnsureSqliteColumnAsync(db, "crs_beneficiary_cache", "relationship_to_head", "TEXT NULL");
            await EnsureSqliteColumnAsync(db, "crs_beneficiary_cache", "is_household_head", "INTEGER NOT NULL DEFAULT 0");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IF NOT EXISTS ix_crs_beneficiary_cache_beneficiary_id
                ON crs_beneficiary_cache (beneficiary_id);");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IF NOT EXISTS ix_crs_beneficiary_cache_residents_id
                ON crs_beneficiary_cache (residents_id);");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IF NOT EXISTS ix_crs_beneficiary_cache_family_id
                ON crs_beneficiary_cache (family_id);");
        }

        private static async Task EnsureDistributionTablesAsync(eSureHiDbContext db)
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS distribution_batches (
                    batch_id INTEGER NOT NULL CONSTRAINT PK_distribution_batches PRIMARY KEY AUTOINCREMENT,
                    project_code TEXT NOT NULL,
                    project_title TEXT NOT NULL,
                    project_description TEXT NULL,
                    source_fund_id INTEGER NULL,
                    amount_per_beneficiary TEXT NOT NULL,
                    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS distribution_records (
                    record_id INTEGER NOT NULL CONSTRAINT PK_distribution_records PRIMARY KEY AUTOINCREMENT,
                    batch_id INTEGER NOT NULL,
                    beneficiary_id INTEGER NOT NULL,
                    status TEXT NOT NULL DEFAULT 'Unreleased',
                    remarks TEXT NULL,
                    processed_at TEXT NULL,
                    fund_debited INTEGER NOT NULL DEFAULT 0
                );");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IF NOT EXISTS ix_distribution_batches_source_fund_id
                ON distribution_batches (source_fund_id);");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IF NOT EXISTS ix_distribution_records_batch_id
                ON distribution_records (batch_id);");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IF NOT EXISTS ix_distribution_records_beneficiary_id
                ON distribution_records (beneficiary_id);");

            // Existing local DBs created before the double-debit guard need the column added.
            await EnsureSqliteColumnAsync(db, "distribution_records", "fund_debited", "INTEGER NOT NULL DEFAULT 0");
        }

        private static async Task EnsurePaymentsTableAsync(eSureHiDbContext db)
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS payments (
                    payment_id INTEGER NOT NULL CONSTRAINT PK_payments PRIMARY KEY AUTOINCREMENT,
                    beneficiary_id INTEGER NOT NULL,
                    family_id TEXT NULL,
                    member_name TEXT NOT NULL,
                    dependent_name TEXT NULL,
                    relationship TEXT NULL,
                    billing_month TEXT NOT NULL,
                    amount TEXT NOT NULL,
                    status TEXT NOT NULL DEFAULT 'Pending',
                    payment_type TEXT NOT NULL DEFAULT 'Advance',
                    source_of_funds TEXT NULL,
                    paid_at TEXT NULL,
                    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    created_by TEXT NULL,
                    remarks TEXT NULL
                );");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IF NOT EXISTS ix_payments_beneficiary_id
                ON payments (beneficiary_id);");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE INDEX IF NOT EXISTS ix_payments_family_id
                ON payments (family_id);");
        }

        private static async Task EnsureSyncColumnsAsync(eSureHiDbContext db)
        {
            foreach (var clrType in eSureHiDbContext.SyncEntityTypes)
            {
                var entityType = db.Model.FindEntityType(clrType);
                var tableName = entityType?.GetTableName();
                if (string.IsNullOrWhiteSpace(tableName))
                    continue;

                var hasTable = await ScalarAsync<long>(
                    db,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = {0};",
                    tableName) > 0;
                if (!hasTable)
                    continue;

                var hasSyncId = await ScalarAsync<long>(
                    db,
                    $"SELECT COUNT(*) FROM pragma_table_info('{Escape(tableName)}') WHERE name = 'SyncId';") > 0;
                if (!hasSyncId)
                    await ExecuteRawCommandAsync(db, $"ALTER TABLE \"{Escape(tableName)}\" ADD COLUMN \"SyncId\" TEXT;");

                var indexName = $"ux_{tableName}_syncid";
                await ExecuteRawCommandAsync(
                    db,
                    $"CREATE UNIQUE INDEX IF NOT EXISTS \"{Escape(indexName)}\" ON \"{Escape(tableName)}\" (\"SyncId\");");
            }
        }

        private static async Task BackfillSyncIdsAsync(eSureHiDbContext db)
        {
            foreach (var clrType in eSureHiDbContext.SyncEntityTypes)
            {
                var entityType = db.Model.FindEntityType(clrType);
                var tableName = entityType?.GetTableName();
                if (string.IsNullOrWhiteSpace(tableName))
                    continue;

                await ExecuteRawCommandAsync(db, $@"
                    UPDATE ""{Escape(tableName)}""
                    SET ""SyncId"" = lower(
                        hex(randomblob(4)) || '-' ||
                        hex(randomblob(2)) || '-' ||
                        '4' || substr(hex(randomblob(2)), 2) || '-' ||
                        substr('89ab', 1 + abs(random()) % 4, 1) || substr(hex(randomblob(2)), 2) || '-' ||
                        hex(randomblob(6)))
                    WHERE ""SyncId"" IS NULL OR ""SyncId"" = '';");
            }
        }

        private static async Task EnsureLocalViewsAsync(eSureHiDbContext db)
        {
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE VIEW IF NOT EXISTS vw_employee_coverage AS
                SELECT
                    ep.ep_id,
                    ep.emp_id,
                    e.employee_no,
                    trim(e.first_name || ' ' || ifnull(e.middle_name || ' ', '') || e.last_name || ifnull(' ' || e.suffix, '')) AS full_name,
                    d.dept_name,
                    e.barangay,
                    p.policy_name,
                    p.policy_type,
                    ep.coverage_limit,
                    ep.employee_share,
                    ep.employer_share,
                    ep.start_date,
                    ep.end_date,
                    ep.assignment_status
                FROM employee_policies ep
                LEFT JOIN employees e ON e.emp_id = ep.emp_id
                LEFT JOIN departments d ON d.dept_id = e.dept_id
                LEFT JOIN insurance_policies p ON p.policy_id = ep.policy_id;");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE VIEW IF NOT EXISTS vw_claims_summary AS
                SELECT
                    e.emp_id,
                    e.employee_no,
                    trim(e.first_name || ' ' || ifnull(e.middle_name || ' ', '') || e.last_name || ifnull(' ' || e.suffix, '')) AS full_name,
                    d.dept_name,
                    e.barangay,
                    count(c.claim_id) AS total_claims,
                    coalesce(sum(c.amount_claimed), 0) AS total_claimed,
                    coalesce(sum(c.amount_approved), 0) AS total_approved,
                    coalesce(sum(c.amount_released), 0) AS total_released,
                    sum(case when c.claim_status IN ('Submitted', 'Under Review') then 1 else 0 end) AS pending_claims,
                    sum(case when c.claim_status IN ('Approved', 'Partially Approved', 'Released') then 1 else 0 end) AS approved_claims,
                    sum(case when c.claim_status = 'Rejected' then 1 else 0 end) AS rejected_claims
                FROM employees e
                LEFT JOIN departments d ON d.dept_id = e.dept_id
                LEFT JOIN claims c ON c.emp_id = e.emp_id
                GROUP BY e.emp_id, e.employee_no, e.first_name, e.middle_name, e.last_name, e.suffix, d.dept_name, e.barangay;");

            await db.Database.ExecuteSqlRawAsync(@"
                CREATE VIEW IF NOT EXISTS vw_premium_status AS
                SELECT
                    pr.premium_id,
                    pr.billing_month,
                    pr.due_date,
                    trim(e.first_name || ' ' || ifnull(e.middle_name || ' ', '') || e.last_name || ifnull(' ' || e.suffix, '')) AS employee_name,
                    p.policy_name,
                    coalesce(pr.total_amount, pr.employee_amount + pr.employer_amount) AS total_amount,
                    pr.amount_paid,
                    pr.balance,
                    pr.payment_status
                FROM premiums pr
                LEFT JOIN employee_policies ep ON ep.ep_id = pr.ep_id
                LEFT JOIN employees e ON e.emp_id = ep.emp_id
                LEFT JOIN insurance_policies p ON p.policy_id = ep.policy_id;");
        }

        private static async Task<T> ScalarAsync<T>(
            eSureHiDbContext db,
            string sql,
            params object[] parameters)
        {
            var conn = db.Database.GetDbConnection();
            await db.Database.OpenConnectionAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = cmd.CreateParameter();
                parameter.ParameterName = $"@p{i}";
                parameter.Value = parameters[i];
                cmd.Parameters.Add(parameter);
                cmd.CommandText = cmd.CommandText.Replace($"{{{i}}}", parameter.ParameterName);
            }

            var value = await cmd.ExecuteScalarAsync();
            return value is null || value is DBNull
                ? default!
                : (T)Convert.ChangeType(value, typeof(T));
        }

        private static async Task ExecuteRawCommandAsync(eSureHiDbContext db, string sql)
        {
            var conn = db.Database.GetDbConnection();
            await db.Database.OpenConnectionAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task EnsureClaimColumnsAsync(eSureHiDbContext db)
        {
            // New claim-breakdown columns (added Phase 6). Idempotent for existing DBs;
            // fresh DBs already get these from EnsureCreatedAsync via the model.
            await EnsureSqliteColumnAsync(db, "claims", "admission_date", "TEXT NULL");
            await EnsureSqliteColumnAsync(db, "claims", "discharge_date", "TEXT NULL");
            await EnsureSqliteColumnAsync(db, "claims", "excess_bill_amount", "TEXT NOT NULL DEFAULT '0'");
            await EnsureSqliteColumnAsync(db, "claims", "outside_diagnostics_amount", "TEXT NOT NULL DEFAULT '0'");
            await EnsureSqliteColumnAsync(db, "claims", "total_covered", "TEXT NOT NULL DEFAULT '0'");
        }

        private static async Task EnsureSqliteColumnAsync(
            eSureHiDbContext db,
            string tableName,
            string columnName,
            string columnDefinition)
        {
            var hasColumn = await ScalarAsync<long>(
                db,
                $"SELECT COUNT(*) FROM pragma_table_info('{Escape(tableName)}') WHERE name = {{0}};",
                columnName) > 0;
            if (hasColumn)
                return;

            await ExecuteRawCommandAsync(
                db,
                $"ALTER TABLE \"{Escape(tableName)}\" ADD COLUMN \"{Escape(columnName)}\" {columnDefinition};");
        }

        private static string Escape(string value) =>
            value.Replace("\"", "\"\"", StringComparison.Ordinal);

    }
}
