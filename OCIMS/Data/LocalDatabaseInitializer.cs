using System;
using System.Linq;
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
            await EnsureSyncColumnsAsync(db);
            await EnsureLocalViewsAsync(db);
            await BackfillSyncIdsAsync(db);
            await SeedSampleDataAsync(db);
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

        private static async Task SeedSampleDataAsync(eSureHiDbContext db)
        {
            if (await db.Beneficiaries.AnyAsync())
            {
                db.Beneficiaries.RemoveRange(db.Beneficiaries);
                await db.SaveChangesAsync();
            }

            if (await db.Employees.AnyAsync()) return;

            var employees = new[]
            {
                new eSureHi.Models.Employee { EmployeeNo = "EMP-001", FirstName = "Juan", LastName = "Dela Cruz", EmploymentType = "Regular", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new eSureHi.Models.Employee { EmployeeNo = "EMP-002", FirstName = "Maria", LastName = "Clara", EmploymentType = "Casual", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new eSureHi.Models.Employee { EmployeeNo = "EMP-003", FirstName = "Jose", LastName = "Rizal", EmploymentType = "Job Order", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new eSureHi.Models.Employee { EmployeeNo = "EMP-004", FirstName = "Andres", LastName = "Bonifacio", EmploymentType = "Regular", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                new eSureHi.Models.Employee { EmployeeNo = "EMP-005", FirstName = "Emilio", LastName = "Aguinaldo", EmploymentType = "Casual", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now }
            };

            db.Employees.AddRange(employees);
            await db.SaveChangesAsync();

            var policy = await db.InsurancePolicies.FirstOrDefaultAsync();
            if (policy != null)
            {
                var claims = new[]
                {
                    new eSureHi.Models.Claim { ClaimNo = "CLM-001", EmpId = employees[0].EmpId, PolicyId = policy.PolicyId, ClaimType = "Medical", AmountClaimed = 5000, ClaimStatus = "Submitted", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                    new eSureHi.Models.Claim { ClaimNo = "CLM-002", EmpId = employees[1].EmpId, PolicyId = policy.PolicyId, ClaimType = "Accident", AmountClaimed = 10000, ClaimStatus = "Approved", AmountApproved = 10000, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                    new eSureHi.Models.Claim { ClaimNo = "CLM-003", EmpId = employees[2].EmpId, PolicyId = policy.PolicyId, ClaimType = "Death", AmountClaimed = 50000, ClaimStatus = "Released", AmountReleased = 50000, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                    new eSureHi.Models.Claim { ClaimNo = "CLM-004", EmpId = employees[3].EmpId, PolicyId = policy.PolicyId, ClaimType = "Medical", AmountClaimed = 2000, ClaimStatus = "Submitted", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now },
                    new eSureHi.Models.Claim { ClaimNo = "CLM-005", EmpId = employees[4].EmpId, PolicyId = policy.PolicyId, ClaimType = "Accident", AmountClaimed = 15000, ClaimStatus = "Under Review", CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now }
                };
                db.Claims.AddRange(claims);
                await db.SaveChangesAsync();
            }

            // Dummy CRS Cache removed to use remote CRS database instead

        }
    }
}
