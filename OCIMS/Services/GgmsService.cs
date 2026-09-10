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
        private const string OfficeCode = "OFF-2026-0004";

        public static async Task<(bool IsOnline, string Message)> CheckReleaseConnectionAsync()
        {
            try
            {
                var connStr = GgmsDbContextFactory.GetConnectionString();
                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();

                foreach (var table in new[] { "yearlybudgets", "officeallocations", "consolidated_transactions", "project_details" })
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
                    INNER JOIN officeallocations a ON a.YearlyBudgetId = y.Id
                    WHERE y.Year = @year AND a.office_code = @office_code", conn);
                allocationCmd.Parameters.AddWithValue("@year", DateTime.Today.Year);
                allocationCmd.Parameters.AddWithValue("@office_code", OfficeCode);

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
                    .Where(a => a.OfficeCode == OfficeCode && a.YearlyBudgetId == budget.Id)
                    .FirstOrDefaultAsync();

                var projectDetail = await db.ProjectDetails
                    .Where(p => p.OfficeCode == OfficeCode && p.YearlyBudgetId == budget.Id)
                    .OrderByDescending(p => p.Id)
                    .FirstOrDefaultAsync();

                if (alloc is null && projectDetail is null)
                {
                    summary.ErrorMessage = $"No allocation found for office {OfficeCode}.";
                    return summary;
                }

                summary.Year = budget.Year;
                summary.AllocatedAmount = projectDetail?.TotalBudget ?? (alloc?.AllocatedAmount ?? 0);
                summary.SpentAmount = alloc?.SpentAmount ?? 0;
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

        public static async Task<(bool Success, string Message)> RecordDistributionReleaseAsync(
            int distributionRecordId,
            string? beneficiaryIdentity,
            string? civilRegistryId,
            decimal amountReleased,
            string programType,
            string firstName,
            string? middleName,
            string lastName,
            string fullName,
            string batchName,
            string? sourceOfFunds = null)
        {
            return await RecordConsolidatedTransactionAsync(
                projectCode: BuildProjectCode(distributionRecordId),
                projectName: AddSourceSuffix(string.IsNullOrWhiteSpace(batchName) ? "Pension Distribution" : batchName, sourceOfFunds),
                beneficiaryId: beneficiaryIdentity,
                civilRegistryId: civilRegistryId,
                firstName: firstName,
                middleName: middleName,
                lastName: lastName,
                fullName: fullName,
                transactionType: string.IsNullOrWhiteSpace(programType) ? "Pension Distribution" : programType,
                amount: amountReleased,
                transactionDate: DateOnly.FromDateTime(DateTime.Today));
        }

        private static string BuildProjectCode(int id) =>
            $"{ProjectPrefix}-{Math.Max(0, id):000000}";

        private static string AddSourceSuffix(string text, string? sourceOfFunds)
        {
            if (string.IsNullOrWhiteSpace(sourceOfFunds))
                return text;

            return $"{text} - {sourceOfFunds.Trim()}";
        }

        public static async Task SyncQueueAsync()
        {
            try
            {
                var connectionTest = await CheckReleaseConnectionAsync();
                if (!connectionTest.IsOnline)
                    return; // GGMS is offline, do nothing.

                await using var localDb = eSureHiDbContextFactory.Create();
                var pendingItems = await localDb.GgmsQueueItems
                    .Where(q => q.Status == "Pending" || q.Status == "Failed")
                    .OrderBy(q => q.CreatedAt)
                    .ToListAsync();

                if (pendingItems.Count == 0)
                    return;

                var connStr = GgmsDbContextFactory.GetConnectionString();
                using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();

                // Load dynamic project details for the current year
                string dynamicProjectCode = "IMS";
                string dynamicProjectName = "Insurance Management System";
                using (var detailCmd = new MySqlCommand(@"
                    SELECT project_details_id, project
                    FROM project_details
                    WHERE office_code = @office_code AND yearly_budget_id = (
                        SELECT Id FROM yearlybudgets WHERE Year = @year ORDER BY Id DESC LIMIT 1
                    )
                    ORDER BY id DESC LIMIT 1", conn))
                {
                    detailCmd.Parameters.AddWithValue("@office_code", OfficeCode);
                    detailCmd.Parameters.AddWithValue("@year", DateTime.Today.Year);
                    using (var detailReader = await detailCmd.ExecuteReaderAsync())
                    {
                        if (await detailReader.ReadAsync())
                        {
                            dynamicProjectCode = detailReader.GetString(0);
                            dynamicProjectName = detailReader.GetString(1);
                        }
                    }
                }

                foreach (var item in pendingItems)
                {
                    await using var transaction = await conn.BeginTransactionAsync();
                    try
                    {
                        var transDate = DateOnly.FromDateTime(item.TransactionDate);

                        // Resolve dynamic code and name
                        var resolvedProjectCode = ResolveProjectCode(item.ProjectCode, dynamicProjectCode);
                        var resolvedProjectName = Truncate($"{dynamicProjectName} - {item.ProjectName}", 45) ?? dynamicProjectName;

                        // Prevent duplicates by checking if the transaction is already uploaded to GGMS
                        using (var checkCmd = new MySqlCommand(
                            "SELECT COUNT(*) FROM consolidated_transactions WHERE project_code = @code", conn, transaction))
                        {
                            checkCmd.Parameters.AddWithValue("@code", resolvedProjectCode);
                            var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                            if (exists)
                            {
                                await transaction.CommitAsync();
                                item.Status = "Success";
                                item.ErrorMessage = null;
                                item.UpdatedAt = DateTime.Now;
                                continue;
                            }
                        }

                        CurrentAllocation? allocation = null;
                        if (item.Amount > 0)
                        {
                            allocation = await LoadCurrentAllocationForUpdateAsync(conn, transaction);
                            if (allocation is null)
                            {
                                throw new InvalidOperationException($"No GGMS fund allocation found for {OfficeCode} in {DateTime.Today.Year}.");
                            }

                            decimal dynamicBudget = allocation.AllocatedAmount;
                            using (var budgetCmd = new MySqlCommand(@"
                                SELECT total_budget FROM project_details
                                WHERE office_code = @office_code AND yearly_budget_id = (
                                    SELECT YearlyBudgetId FROM officeallocations WHERE Id = @alloc_id
                                )
                                ORDER BY id DESC LIMIT 1", conn, transaction))
                            {
                                budgetCmd.Parameters.AddWithValue("@office_code", OfficeCode);
                                budgetCmd.Parameters.AddWithValue("@alloc_id", allocation.Id);
                                var budgetVal = await budgetCmd.ExecuteScalarAsync();
                                if (budgetVal != null && budgetVal != DBNull.Value)
                                {
                                    dynamicBudget = Convert.ToDecimal(budgetVal);
                                }
                            }

                            var remaining = dynamicBudget - allocation.SpentAmount;
                            if (item.Amount > remaining)
                            {
                                throw new InvalidOperationException($"Insufficient GGMS funds. Remaining: {remaining:N2}, requested: {item.Amount:N2}.");
                            }
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

                        cmd.Parameters.AddWithValue("@beneficiary_id", (object?)Truncate(item.BeneficiaryId, 45) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@civil_registry_id", (object?)Truncate(item.CivilRegistryId, 45) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@project_code", Truncate(resolvedProjectCode, 45)!);
                        cmd.Parameters.AddWithValue("@project_name", Truncate(resolvedProjectName, 45)!);
                        cmd.Parameters.AddWithValue("@office_id", OfficeCode);
                        cmd.Parameters.AddWithValue("@full_name", Truncate(item.FullName, 45)!);
                        cmd.Parameters.AddWithValue("@first_name", Truncate(item.FirstName, 45)!);
                        cmd.Parameters.AddWithValue("@middle_name", (object?)Truncate(item.MiddleName, 45) ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@last_name", Truncate(item.LastName, 45)!);
                        cmd.Parameters.AddWithValue("@office_name", Truncate(dynamicProjectName, 45) ?? OfficeName);
                        cmd.Parameters.AddWithValue("@transaction_type", Truncate(item.TransactionType, 45)!);
                        cmd.Parameters.AddWithValue("@amount", item.Amount);
                        cmd.Parameters.AddWithValue("@transaction_date", transDate.ToString("yyyy-MM-dd"));

                        await cmd.ExecuteNonQueryAsync();

                        if (item.Amount > 0 && allocation != null)
                        {
                            using var updateCmd = new MySqlCommand(@"
                                UPDATE officeallocations
                                SET SpentAmount = SpentAmount + @amount
                                WHERE Id = @allocation_id",
                                conn, transaction);
                            updateCmd.Parameters.AddWithValue("@amount", item.Amount);
                            updateCmd.Parameters.AddWithValue("@allocation_id", allocation.Id);
                            await updateCmd.ExecuteNonQueryAsync();
                        }

                        await transaction.CommitAsync();

                        item.Status = "Success";
                        item.ErrorMessage = null;
                        item.UpdatedAt = DateTime.Now;
                    }
                    catch (Exception itemEx)
                    {
                        await transaction.RollbackAsync();
                        item.Status = "Failed";
                        item.ErrorMessage = itemEx.Message;
                        item.UpdatedAt = DateTime.Now;
                    }
                }

                await localDb.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GGMS Queue Sync failed: {ex.Message}");
            }
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

                // Load dynamic project details for the transaction's year
                string dynamicProjectCode = "IMS";
                string dynamicProjectName = "Insurance Management System";
                using (var detailCmd = new MySqlCommand(@"
                    SELECT project_details_id, project
                    FROM project_details
                    WHERE office_code = @office_code AND yearly_budget_id = (
                        SELECT Id FROM yearlybudgets WHERE Year = @year ORDER BY Id DESC LIMIT 1
                    )
                    ORDER BY id DESC LIMIT 1", conn, transaction))
                {
                    detailCmd.Parameters.AddWithValue("@office_code", OfficeCode);
                    detailCmd.Parameters.AddWithValue("@year", transactionDate.Year);
                    using (var detailReader = await detailCmd.ExecuteReaderAsync())
                    {
                        if (await detailReader.ReadAsync())
                        {
                            dynamicProjectCode = detailReader.GetString(0);
                            dynamicProjectName = detailReader.GetString(1);
                        }
                    }
                }

                // Resolve dynamic code and name
                var resolvedProjectCode = ResolveProjectCode(projectCode, dynamicProjectCode);
                var resolvedProjectName = Truncate($"{dynamicProjectName} - {projectName}", 45) ?? dynamicProjectName;

                // Prevent duplicates by checking if the transaction is already uploaded to GGMS
                using (var checkCmd = new MySqlCommand(
                    "SELECT COUNT(*) FROM consolidated_transactions WHERE project_code = @code", conn, transaction))
                {
                    checkCmd.Parameters.AddWithValue("@code", resolvedProjectCode);
                    var exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                    if (exists)
                    {
                        await transaction.CommitAsync();
                        return (true, "GGMS transaction already recorded.");
                    }
                }

                var allocation = await LoadCurrentAllocationForUpdateAsync(conn, transaction);
                if (allocation is null)
                {
                    await transaction.RollbackAsync();
                    return (false, $"No GGMS fund allocation found for {OfficeCode} in {DateTime.Today.Year}.");
                }

                decimal dynamicBudget = allocation.AllocatedAmount;
                using (var budgetCmd = new MySqlCommand(@"
                    SELECT total_budget FROM project_details
                    WHERE office_code = @office_code AND yearly_budget_id = (
                        SELECT YearlyBudgetId FROM officeallocations WHERE Id = @alloc_id
                    )
                    ORDER BY id DESC LIMIT 1", conn, transaction))
                {
                    budgetCmd.Parameters.AddWithValue("@office_code", OfficeCode);
                    budgetCmd.Parameters.AddWithValue("@alloc_id", allocation.Id);
                    var budgetVal = await budgetCmd.ExecuteScalarAsync();
                    if (budgetVal != null && budgetVal != DBNull.Value)
                    {
                        dynamicBudget = Convert.ToDecimal(budgetVal);
                    }
                }

                var remaining = dynamicBudget - allocation.SpentAmount;
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
                cmd.Parameters.AddWithValue("@project_code", Truncate(resolvedProjectCode, 45)!);
                cmd.Parameters.AddWithValue("@project_name", Truncate(resolvedProjectName, 45)!);
                cmd.Parameters.AddWithValue("@office_id", OfficeCode);
                cmd.Parameters.AddWithValue("@full_name", Truncate(fullName, 45)!);
                cmd.Parameters.AddWithValue("@first_name", Truncate(firstName, 45)!);
                cmd.Parameters.AddWithValue("@middle_name", (object?)Truncate(middleName, 45) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@last_name", Truncate(lastName, 45)!);
                cmd.Parameters.AddWithValue("@office_name", Truncate(dynamicProjectName, 45) ?? OfficeName);
                cmd.Parameters.AddWithValue("@transaction_type", Truncate(transactionType, 45)!);
                cmd.Parameters.AddWithValue("@amount", amount);
                cmd.Parameters.AddWithValue("@transaction_date", transactionDate.ToString("yyyy-MM-dd"));

                await cmd.ExecuteNonQueryAsync();

                using var updateCmd = new MySqlCommand(@"
                    UPDATE officeallocations
                    SET SpentAmount = SpentAmount + @amount
                    WHERE Id = @allocation_id",
                    conn, transaction);
                updateCmd.Parameters.AddWithValue("@amount", amount);
                updateCmd.Parameters.AddWithValue("@allocation_id", allocation.Id);
                await updateCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                return (true, "GGMS funds received and consolidation recorded.");
            }
            catch (Exception ex)
            {
                // Queue the transaction locally!
                try
                {
                    await using var localDb = eSureHiDbContextFactory.Create();
                    var queueItem = new GgmsQueueItem
                    {
                        ProjectCode = projectCode,
                        ProjectName = projectName,
                        BeneficiaryId = beneficiaryId,
                        CivilRegistryId = civilRegistryId,
                        FirstName = firstName,
                        MiddleName = middleName,
                        LastName = lastName,
                        FullName = fullName,
                        TransactionType = transactionType,
                        Amount = amount,
                        TransactionDate = new DateTime(transactionDate.Year, transactionDate.Month, transactionDate.Day),
                        Status = "Pending",
                        ErrorMessage = ex.Message,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    localDb.GgmsQueueItems.Add(queueItem);
                    await localDb.SaveChangesAsync();

                    return (true, $"Queued locally (GGMS offline: {ex.Message})");
                }
                catch (Exception dbEx)
                {
                    return (false, $"Failed to record to GGMS and failed to save to local queue: {dbEx.Message}");
                }
            }
        }

        private sealed class CurrentAllocation
        {
            public int Id { get; set; }
            public decimal AllocatedAmount { get; set; }
            public decimal SpentAmount { get; set; }
        }

        private static async Task<CurrentAllocation?> LoadCurrentAllocationForUpdateAsync(
            MySqlConnection conn,
            MySqlTransaction transaction)
        {
            using var cmd = new MySqlCommand(@"
                SELECT a.Id, a.AllocatedAmount, a.SpentAmount
                FROM yearlybudgets y
                INNER JOIN officeallocations a ON a.YearlyBudgetId = y.Id
                WHERE y.Year = @year AND a.office_code = @office_code
                ORDER BY y.Id DESC, a.Id DESC
                LIMIT 1
                FOR UPDATE",
                conn, transaction);
            cmd.Parameters.AddWithValue("@year", DateTime.Today.Year);
            cmd.Parameters.AddWithValue("@office_code", OfficeCode);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return null;

            var idOrdinal = reader.GetOrdinal("Id");
            var allocOrdinal = reader.GetOrdinal("AllocatedAmount");
            var spentOrdinal = reader.GetOrdinal("SpentAmount");

            return new CurrentAllocation
            {
                Id = reader.IsDBNull(idOrdinal) ? 0 : reader.GetInt32(idOrdinal),
                AllocatedAmount = reader.IsDBNull(allocOrdinal) ? 0 : reader.GetDecimal(allocOrdinal),
                SpentAmount = reader.IsDBNull(spentOrdinal) ? 0 : reader.GetDecimal(spentOrdinal)
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

        private static string ResolveProjectCode(string originalCode, string dynamicDetailsId)
        {
            if (string.IsNullOrWhiteSpace(originalCode))
                return dynamicDetailsId;

            var parts = originalCode.Split('-');
            var suffix = parts.Length > 1 ? parts[^1] : originalCode;
            return $"{dynamicDetailsId}-{suffix}";
        }
    }
}
