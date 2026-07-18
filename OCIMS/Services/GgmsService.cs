using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Models;
using MySqlConnector;

namespace eSureHi.Services
{
    public class GgmsFundSummary
    {
        public int Year { get; set; }
        public decimal AllocatedAmount { get; set; }
        public decimal SpentAmount { get; set; }
        public decimal RemainingAmount => AllocatedAmount - SpentAmount;
        public bool IsLoaded { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public static class GgmsService
    {
        private const string ProjectPrefix = "IMS";
        private const string OfficeName = "Insurance Management System";
        // String office code — used ONLY for consolidated_transactions.office_id
        // (a varchar column) and the local ggms_allocation_cache. Never use this
        // against budget_allocations.
        private const string OfficeCode = "OFF-2026-0004";
        // Numeric office id — used for ALL budget_allocations queries.
        // budget_allocations.office_id is bigint and logically references
        // tbl_offices(id) (no DB-level FK exists). Confirmed against live GGMS:
        // tbl_offices id 15 = "Insurance" (OFF-2026-0004).
        private const long OfficeIdValue = 15;

        public static async Task<(bool IsOnline, string Message)> CheckReleaseConnectionAsync()
        {
            try
            {
                var connStr = GgmsDbContextFactory.GetConnectionString();
                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();

                foreach (var table in new[] { "yearlybudgets", "budget_allocations", "consolidated_transactions" })
                {
                    using var tableCmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM information_schema.tables " +
                        "WHERE table_schema = DATABASE() AND table_name = @table", conn);
                    tableCmd.Parameters.AddWithValue("@table", table);
                    var count = Convert.ToInt32(await tableCmd.ExecuteScalarAsync());
                    if (count == 0)
                        return (false, $"GGMS table '{table}' was not found.");
                }

                using var allocationCmd = new MySqlCommand(@"
                    SELECT COUNT(*)
                    FROM yearlybudgets y
                    INNER JOIN budget_allocations a ON a.master_budget_id = y.Id
                    WHERE y.Year = @year AND a.office_id = @office_id", conn);
                allocationCmd.Parameters.AddWithValue("@year", DateTime.Today.Year);
                allocationCmd.Parameters.AddWithValue("@office_id", OfficeIdValue);

                var allocationCount = Convert.ToInt32(await allocationCmd.ExecuteScalarAsync());
                if (allocationCount == 0)
                    return (false, $"No GGMS fund allocation found for {OfficeCode} in {DateTime.Today.Year}.");

                return (true, "Online GGMS fund connection is available.");
            }
            catch (Exception ex)
            {
                return (false, $"Online connection required. GGMS/Hostinger is unavailable: {ex.Message}");
            }
        }

        public static async Task<GgmsFundSummary> GetFundSummaryAsync()
        {
            var summary = new GgmsFundSummary();
            try
            {
                using var db = GgmsDbContextFactory.Create();

                var budget = await db.YearlyBudgets
                    .Where(y => y.Year == DateTime.Today.Year)
                    .OrderByDescending(y => y.Id)
                    .FirstOrDefaultAsync();

                if (budget is null)
                {
                    summary.ErrorMessage = "No yearly budget found for current year.";
                    return summary;
                }

                var alloc = await db.BudgetAllocations
                    .Where(a => a.OfficeId == OfficeIdValue && a.MasterBudgetId == budget.Id)
                    .FirstOrDefaultAsync();

                if (alloc is null)
                {
                    summary.ErrorMessage = $"No allocation found for office {OfficeIdValue}.";
                    return summary;
                }

                summary.Year = budget.Year;
                summary.AllocatedAmount = alloc.Amount;
                summary.SpentAmount = alloc.UsedAmount;
                summary.IsLoaded = true;
                await SaveAllocationCacheAsync(summary);
                return summary;
            }
            catch (Exception ex)
            {
                var cached = await LoadAllocationCacheAsync();
                if (cached.IsLoaded)
                {
                    cached.ErrorMessage = $"Using cached GGMS allocation. Live GGMS failed: {ex.Message}";
                    return cached;
                }

                summary.ErrorMessage = $"GGMS connection failed: {ex.Message}";
            }

            return summary;
        }

        public static async Task<bool> RefreshAllocationCacheAsync()
        {
            var summary = await GetFundSummaryAsync();
            return summary.IsLoaded;
        }

        public static async Task<(bool Success, string Message)> RecordClaimReleaseAsync(
            int claimId,
            string? beneficiaryIdentity,
            string? civilRegistryId,
            decimal amountReleased,
            string claimType,
            string firstName,
            string? middleName,
            string lastName,
            string recipientName,
            string claimNo,
            string purpose,
            string? sourceOfFunds = null)
        {
            return await RecordConsolidatedTransactionAsync(
                projectCode: BuildProjectCode(claimId),
                projectName: AddSourceSuffix(string.IsNullOrWhiteSpace(purpose) ? "Insurance Claim" : purpose, sourceOfFunds),
                beneficiaryId: beneficiaryIdentity,
                civilRegistryId: civilRegistryId,
                firstName: firstName,
                middleName: middleName,
                lastName: lastName,
                fullName: recipientName,
                transactionType: string.IsNullOrWhiteSpace(claimType) ? "Insurance Claim" : claimType,
                amount: amountReleased,
                transactionDate: DateOnly.FromDateTime(DateTime.Today));
        }

        public static async Task<(bool Success, string Message)> RecordBenefitReleaseAsync(
            int benefitId,
            string? beneficiaryIdentity,
            string? civilRegistryId,
            decimal amountReleased,
            string benefitType,
            string policyName,
            string firstName,
            string? middleName,
            string lastName,
            string fullName,
            DateOnly transactionDate,
            string? sourceOfFunds = null)
        {
            return await RecordConsolidatedTransactionAsync(
                projectCode: BuildProjectCode(benefitId),
                projectName: AddSourceSuffix(string.IsNullOrWhiteSpace(policyName) ? "Insurance Benefit" : policyName, sourceOfFunds),
                beneficiaryId: beneficiaryIdentity,
                civilRegistryId: civilRegistryId,
                firstName: firstName,
                middleName: middleName,
                lastName: lastName,
                fullName: fullName,
                transactionType: string.IsNullOrWhiteSpace(benefitType) ? "Insurance Benefit" : benefitType,
                amount: amountReleased,
                transactionDate: transactionDate);
        }

        private static string BuildProjectCode(int id) =>
            $"{ProjectPrefix}-{Math.Max(0, id):000000}";

        private static string AddSourceSuffix(string text, string? sourceOfFunds)
        {
            if (string.IsNullOrWhiteSpace(sourceOfFunds))
                return text;

            return $"{text} - {sourceOfFunds.Trim()}";
        }

        private static async Task<(bool Success, string Message)> RecordConsolidatedTransactionAsync(
            string projectCode,
            string projectName,
            string? beneficiaryId,
            string? civilRegistryId,
            string firstName,
            string? middleName,
            string lastName,
            string fullName,
            string transactionType,
            decimal amount,
            DateOnly transactionDate)
        {
            try
            {
                var connStr = GgmsDbContextFactory.GetConnectionString();
                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();
                await using var transaction = await conn.BeginTransactionAsync();

                var allocation = await LoadCurrentAllocationForUpdateAsync(conn, transaction);
                if (allocation is null)
                {
                    await transaction.RollbackAsync();
                    return (false, $"No GGMS fund allocation found for {OfficeCode} in {DateTime.Today.Year}.");
                }

                var remaining = allocation.AllocatedAmount - allocation.SpentAmount;
                if (amount > remaining)
                {
                    await transaction.RollbackAsync();
                    return (false, $"Insufficient GGMS funds. Remaining: {remaining:N2}, requested: {amount:N2}.");
                }

                using var cmd = new MySqlCommand(@"
                    INSERT INTO consolidated_transactions
                        (beneficiary_id, civil_registry_id, project_code, project_name,
                         office_id, full_name, first_name, middle_name, last_name,
                         office_name, transaction_type, amount, transaction_date, status)
                    VALUES
                        (@beneficiary_id, @civil_registry_id, @project_code, @project_name,
                         @office_id, @full_name, @first_name, @middle_name, @last_name,
                         @office_name, @transaction_type, @amount, @transaction_date, 'Released')",
                    conn, transaction);

                cmd.Parameters.AddWithValue("@beneficiary_id", (object?)Truncate(beneficiaryId, 45) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@civil_registry_id", (object?)Truncate(civilRegistryId, 45) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@project_code", Truncate(projectCode, 45)!);
                cmd.Parameters.AddWithValue("@project_name", Truncate(projectName, 45)!);
                // consolidated_transactions.office_id is a varchar code, unlike
                // budget_allocations.office_id (bigint) — string OfficeCode is correct here.
                cmd.Parameters.AddWithValue("@office_id", OfficeCode);
                cmd.Parameters.AddWithValue("@full_name", Truncate(fullName, 45)!);
                cmd.Parameters.AddWithValue("@first_name", Truncate(firstName, 45)!);
                cmd.Parameters.AddWithValue("@middle_name", (object?)Truncate(middleName, 45) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@last_name", Truncate(lastName, 45)!);
                cmd.Parameters.AddWithValue("@office_name", OfficeName);
                cmd.Parameters.AddWithValue("@transaction_type", Truncate(transactionType, 45)!);
                cmd.Parameters.AddWithValue("@amount", amount);
                cmd.Parameters.AddWithValue("@transaction_date", transactionDate.ToString("yyyy-MM-dd"));

                await cmd.ExecuteNonQueryAsync();

                using var updateCmd = new MySqlCommand(@"
                    UPDATE budget_allocations
                    SET used_amount = used_amount + @amount
                    WHERE id = @allocation_id",
                    conn, transaction);
                updateCmd.Parameters.AddWithValue("@amount", amount);
                updateCmd.Parameters.AddWithValue("@allocation_id", allocation.Id);
                await updateCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return (true, "GGMS funds received and consolidation recorded.");
            }
            catch (Exception ex)
            {
                return (false, $"GGMS consolidated write failed: {ex.Message}");
            }
        }

        private sealed class CurrentAllocation
        {
            public long Id { get; set; }
            public decimal AllocatedAmount { get; set; }
            public decimal SpentAmount { get; set; }
        }

        private static async Task<CurrentAllocation?> LoadCurrentAllocationForUpdateAsync(
            MySqlConnection conn,
            MySqlTransaction transaction)
        {
            using var cmd = new MySqlCommand(@"
                SELECT a.id, a.amount AS AllocatedAmount, a.used_amount AS SpentAmount
                FROM yearlybudgets y
                INNER JOIN budget_allocations a ON a.master_budget_id = y.Id
                WHERE y.Year = @year AND a.office_id = @office_id
                ORDER BY y.Id DESC, a.id DESC
                LIMIT 1
                FOR UPDATE",
                conn, transaction);
            cmd.Parameters.AddWithValue("@year", DateTime.Today.Year);
            cmd.Parameters.AddWithValue("@office_id", OfficeIdValue);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            return new CurrentAllocation
            {
                Id = reader.GetInt64("id"),
                AllocatedAmount = reader.GetDecimal("AllocatedAmount"),
                SpentAmount = reader.GetDecimal("SpentAmount")
            };
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        private static async Task SaveAllocationCacheAsync(GgmsFundSummary summary)
        {
            await using var db = eSureHiDbContextFactory.Create();
            var existing = await db.GgmsAllocationCache
                .FirstOrDefaultAsync(c => c.OfficeCode == OfficeCode && c.Year == summary.Year);

            if (existing is null)
            {
                existing = new GgmsAllocationCache
                {
                    OfficeCode = OfficeCode,
                    Year = summary.Year
                };
                db.GgmsAllocationCache.Add(existing);
            }

            existing.AllocatedAmount = summary.AllocatedAmount;
            existing.SpentAmount = summary.SpentAmount;
            existing.RemainingAmount = summary.RemainingAmount;
            existing.CachedAt = DateTime.Now;
            await db.SaveChangesAsync();
        }

        private static async Task<GgmsFundSummary> LoadAllocationCacheAsync()
        {
            try
            {
                await using var db = eSureHiDbContextFactory.Create();
                var cached = await db.GgmsAllocationCache
                    .AsNoTracking()
                    .Where(c => c.OfficeCode == OfficeCode)
                    .OrderByDescending(c => c.Year == DateTime.Today.Year)
                    .ThenByDescending(c => c.CachedAt)
                    .FirstOrDefaultAsync();

                if (cached is null)
                    return new GgmsFundSummary();

                return new GgmsFundSummary
                {
                    Year = cached.Year,
                    AllocatedAmount = cached.AllocatedAmount,
                    SpentAmount = cached.SpentAmount,
                    IsLoaded = true,
                    ErrorMessage = $"Using cached GGMS allocation from {cached.CachedAt:g}."
                };
            }
            catch
            {
                return new GgmsFundSummary();
            }
        }
    }
}
