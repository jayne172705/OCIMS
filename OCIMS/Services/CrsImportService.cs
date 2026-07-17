using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;
using eSureHi.Data;
using eSureHi.Models;
using Microsoft.EntityFrameworkCore;

namespace eSureHi.Services
{
    public class CrsImportProgress
    {
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public int Total { get; set; }
        public bool IsRunning { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public static class CrsImportService
    {
        private const int BatchSize = 500;

        public static async Task ImportAsync(
            string crsConnectionString,
            IProgress<CrsImportProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var rows = await FetchRemoteRowsAsync(crsConnectionString, progress, cancellationToken);
                await SaveToLocalStagingAsync(rows, "CRS Hostinger", progress, cancellationToken);
                await ReplaceCacheAsync(rows, cancellationToken);
            }
            catch (Exception remoteEx)
            {
                progress?.Report(new CrsImportProgress
                {
                    IsRunning = true,
                    Message = $"CRS is unavailable, loading local cache: {remoteEx.GetBaseException().Message}"
                });

                var cachedRows = await LoadRowsFromCacheAsync(cancellationToken);
                if (cachedRows.Count == 0)
                    throw;

                await SaveToLocalStagingAsync(cachedRows, "local CRS cache", progress, cancellationToken);
            }
        }

        public static async Task<int> RefreshCacheAsync(
            string crsConnectionString,
            CancellationToken cancellationToken = default)
        {
            var rows = await FetchRemoteRowsAsync(crsConnectionString, null, cancellationToken);
            await ReplaceCacheAsync(rows, cancellationToken);
            return rows.Count;
        }

        private static async Task ReplaceCacheAsync(
            List<BeneficiaryStaging> rows,
            CancellationToken cancellationToken = default)
        {
            await using var db = eSureHiDbContextFactory.Create();
            db.CrsBeneficiaryCache.RemoveRange(db.CrsBeneficiaryCache);
            await db.SaveChangesAsync(cancellationToken);

            var cachedAt = DateTime.Now;
            db.CrsBeneficiaryCache.AddRange(rows.Select(r => new CrsBeneficiaryCache
            {
                BeneficiaryId = r.BeneficiaryId,
                FullName = r.FullName,
                FirstName = r.FirstName,
                LastName = r.LastName,
                MiddleName = r.MiddleName,
                Sex = r.Sex,
                Age = r.Age,
                Address = r.Address,
                DateOfBirth = r.DateOfBirth,
                MaritalStatus = r.MaritalStatus,
                IsPwd = r.IsPwd,
                IsSenior = r.IsSenior,
                CachedAt = cachedAt
            }));
            await db.SaveChangesAsync(cancellationToken);
        }

        private static async Task<List<BeneficiaryStaging>> FetchRemoteRowsAsync(
            string crsConnectionString,
            IProgress<CrsImportProgress>? progress,
            CancellationToken cancellationToken)
        {
            var connStr = crsConnectionString.TrimEnd(';')
                + ";Connection Timeout=10;Default Command Timeout=120;";

            var rows = new List<BeneficiaryStaging>();
            var total = 0;

            await using var countConn = new MySqlConnection(connStr);
            await countConn.OpenAsync(cancellationToken);
            await using (var countCmd = new MySqlCommand("SELECT COUNT(*) FROM val_beneficiaries", countConn))
            {
                total = Convert.ToInt32(await countCmd.ExecuteScalarAsync(cancellationToken));
            }

            progress?.Report(new CrsImportProgress
            {
                Total = total,
                IsRunning = true,
                Message = $"Downloading {total:N0} CRS records..."
            });

            for (var offset = 0; offset < total; offset += BatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await using var crsConn = new MySqlConnection(connStr);
                await crsConn.OpenAsync(cancellationToken);

                var sql = $@"
                    SELECT residents_id, beneficiary_id, civilregistry_id,
                           last_name, first_name, middle_name, full_name,
                           sex, date_of_birth, marital_status, address,
                           is_pwd, pwd_id_no, is_senior, senior_id_no,
                           disability_type, cause_of_disability
                    FROM val_beneficiaries
                    ORDER BY id
                    LIMIT {BatchSize} OFFSET {offset}";

                await using var cmd = new MySqlCommand(sql, crsConn);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var beneficiaryId = ReadString(reader, "beneficiary_id");
                    if (string.IsNullOrWhiteSpace(beneficiaryId))
                        continue;

                    var dobStr = ReadString(reader, "date_of_birth");
                    string computedAge = "";
                    if (DateTime.TryParse(dobStr, out var dob))
                    {
                        var today = DateTime.Today;
                        var age = today.Year - dob.Year;
                        if (dob.Date > today.AddYears(-age)) age--;
                        computedAge = age.ToString();
                    }

                    rows.Add(new BeneficiaryStaging
                    {
                        ResidentsId = ReadLong(reader, "residents_id"),
                        BeneficiaryId = beneficiaryId,
                        CivilRegistryId = ReadString(reader, "civilregistry_id"),
                        LastName = ReadString(reader, "last_name"),
                        FirstName = ReadString(reader, "first_name"),
                        MiddleName = ReadString(reader, "middle_name"),
                        FullName = ReadString(reader, "full_name"),
                        Sex = ReadString(reader, "sex"),
                        DateOfBirth = dobStr,
                        Age = computedAge,
                        MaritalStatus = ReadString(reader, "marital_status"),
                        Address = ReadString(reader, "address"),
                        IsPwd = ReadBool(reader, "is_pwd"),
                        PwdIdNo = ReadString(reader, "pwd_id_no"),
                        IsSenior = ReadBool(reader, "is_senior"),
                        SeniorIdNo = ReadString(reader, "senior_id_no"),
                        DisabilityType = ReadString(reader, "disability_type"),
                        CauseOfDisability = ReadString(reader, "cause_of_disability"),
                        LinkStatus = "Unlinked",
                        ImportedAt = DateTime.Now
                    });
                }

                progress?.Report(new CrsImportProgress
                {
                    Imported = rows.Count,
                    Total = total,
                    IsRunning = true,
                    Message = $"Downloaded {rows.Count:N0} of {total:N0} CRS records..."
                });
            }

            return rows;
        }

        private static async Task<List<BeneficiaryStaging>> LoadRowsFromCacheAsync(CancellationToken cancellationToken)
        {
            await using var db = eSureHiDbContextFactory.Create();
            var cached = await db.CrsBeneficiaryCache
                .AsNoTracking()
                .OrderBy(c => c.BeneficiaryId)
                .ToListAsync(cancellationToken);

            return cached.Select(c => new BeneficiaryStaging
            {
                BeneficiaryId = c.BeneficiaryId,
                LastName = c.LastName,
                FirstName = c.FirstName,
                MiddleName = c.MiddleName,
                FullName = c.FullName,
                Sex = c.Sex,
                DateOfBirth = c.DateOfBirth,
                Age = c.Age,
                MaritalStatus = c.MaritalStatus,
                Address = c.Address,
                IsPwd = c.IsPwd,
                IsSenior = c.IsSenior,
                LinkStatus = "Unlinked",
                ImportedAt = DateTime.Now
            }).ToList();
        }

        private static async Task SaveToLocalStagingAsync(
            List<BeneficiaryStaging> rows,
            string sourceLabel,
            IProgress<CrsImportProgress>? progress,
            CancellationToken cancellationToken)
        {
            await using var db = eSureHiDbContextFactory.Create();
            var existingIds = new HashSet<string>(
                await db.BeneficiaryStaging
                    .Select(b => b.BeneficiaryId)
                    .ToListAsync(cancellationToken),
                StringComparer.OrdinalIgnoreCase);

            var imported = 0;
            var skipped = 0;
            foreach (var batch in rows.Chunk(BatchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var newRows = batch
                    .Where(r => !string.IsNullOrWhiteSpace(r.BeneficiaryId) && existingIds.Add(r.BeneficiaryId))
                    .ToList();

                skipped += batch.Length - newRows.Count;
                if (newRows.Count > 0)
                {
                    db.BeneficiaryStaging.AddRange(newRows);
                    imported += await db.SaveChangesAsync(cancellationToken);
                }

                progress?.Report(new CrsImportProgress
                {
                    Imported = imported,
                    Skipped = skipped,
                    Total = rows.Count,
                    IsRunning = true,
                    Message = $"Imported {imported:N0} from {sourceLabel}; skipped {skipped:N0} duplicates..."
                });
            }

            progress?.Report(new CrsImportProgress
            {
                Imported = imported,
                Skipped = skipped,
                Total = rows.Count,
                IsRunning = false,
                Message = $"Complete - {imported:N0} imported from {sourceLabel}, {skipped:N0} skipped."
            });
        }

        private static string? ReadString(MySqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal)?.ToString();
        }

        private static long? ReadLong(MySqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : Convert.ToInt64(reader.GetValue(ordinal));
        }

        private static bool ReadBool(MySqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal));
        }
    }
}
