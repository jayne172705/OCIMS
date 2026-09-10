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
                ResidentsId = r.ResidentsId,
                CivilRegistryId = r.CivilRegistryId,
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
                FamilyId = r.FamilyId,
                HouseholdId = r.HouseholdId,
                FamilyRole = r.FamilyRole,
                RelationshipToHead = r.RelationshipToHead,
                IsHouseholdHead = r.IsHouseholdHead,
                CedulaNo = r.CedulaNo,
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

            var residenceMap = new Dictionary<string, string>();
            try
            {
                await using var resConn = new MySqlConnection(connStr);
                await resConn.OpenAsync(cancellationToken);
                await using (var resCmd = new MySqlCommand("SELECT certificate_no, resident_first_name, resident_last_name FROM residence_records", resConn))
                await using (var resReader = await resCmd.ExecuteReaderAsync(cancellationToken))
                {
                    var certNoOrdinal = resReader.GetOrdinal("certificate_no");
                    var fnOrdinal = resReader.GetOrdinal("resident_first_name");
                    var lnOrdinal = resReader.GetOrdinal("resident_last_name");
                    while (await resReader.ReadAsync(cancellationToken))
                    {
                        var certNo = resReader.IsDBNull(certNoOrdinal) ? string.Empty : resReader.GetString(certNoOrdinal);
                        var fn = resReader.IsDBNull(fnOrdinal) ? string.Empty : resReader.GetString(fnOrdinal)?.Trim().ToLower() ?? "";
                        var ln = resReader.IsDBNull(lnOrdinal) ? string.Empty : resReader.GetString(lnOrdinal)?.Trim().ToLower() ?? "";
                        var key = $"{fn}|{ln}";
                        if (!string.IsNullOrEmpty(key) && !residenceMap.ContainsKey(key))
                        {
                            residenceMap[key] = certNo;
                        }
                    }
                }
            }
            catch (Exception resEx)
            {
                System.Diagnostics.Debug.WriteLine("Failed to fetch remote residence records: " + resEx.Message);
            }

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
                    SELECT v.residents_id, v.beneficiary_id, v.civilregistry_id,
                           v.last_name, v.first_name, v.middle_name, v.full_name,
                           v.sex, v.date_of_birth, v.marital_status, v.address,
                           v.is_pwd, v.pwd_id_no, v.is_senior, v.senior_id_no,
                           v.disability_type, v.cause_of_disability,
                           d.position, d.family_id, d.household_number, d.relationship_to_head
                    FROM val_beneficiaries v
                    LEFT JOIN demographic_characteristics d ON v.residents_id = d.id
                    ORDER BY v.id
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

                    var familyRole = ReadString(reader, "position");
                    var normalizedRole = familyRole?.Replace('_', ' ').Trim() ?? "";
                    bool isHead = !string.IsNullOrWhiteSpace(normalizedRole) && 
                                  (normalizedRole.Equals("head of the family", StringComparison.OrdinalIgnoreCase) ||
                                   normalizedRole.Equals("family head", StringComparison.OrdinalIgnoreCase) ||
                                   normalizedRole.Equals("head of family", StringComparison.OrdinalIgnoreCase) ||
                                   normalizedRole.Equals("household head", StringComparison.OrdinalIgnoreCase) ||
                                   normalizedRole.Equals("head", StringComparison.OrdinalIgnoreCase));

                    var fnLower = (ReadString(reader, "first_name") ?? "").Trim().ToLower();
                    var lnLower = (ReadString(reader, "last_name") ?? "").Trim().ToLower();
                    var resKey = $"{fnLower}|{lnLower}";
                    string? matchedCedula = null;
                    if (residenceMap.TryGetValue(resKey, out var cNo))
                    {
                        matchedCedula = cNo;
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
                        FamilyRole = familyRole,
                        FamilyId = ReadString(reader, "family_id"),
                        HouseholdId = ReadString(reader, "household_number"),
                        RelationshipToHead = ReadString(reader, "relationship_to_head"),
                        IsHouseholdHead = isHead,
                        HasDemographicProfile = !string.IsNullOrWhiteSpace(ReadString(reader, "family_id")),
                        DemographicFamilyId = ReadString(reader, "family_id") ?? "",
                        DemographicFamilyRole = familyRole ?? "",
                        DemographicRelationshipToHead = ReadString(reader, "relationship_to_head") ?? "",
                        IsDemographicHeadOfFamily = isHead,
                        LinkStatus = "Unlinked",
                        CedulaNo = matchedCedula,
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
                ResidentsId = c.ResidentsId,
                BeneficiaryId = c.BeneficiaryId,
                CivilRegistryId = c.CivilRegistryId,
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
                FamilyId = c.FamilyId,
                HouseholdId = c.HouseholdId,
                FamilyRole = c.FamilyRole,
                RelationshipToHead = c.RelationshipToHead,
                IsHouseholdHead = c.IsHouseholdHead,
                HasDemographicProfile = !string.IsNullOrWhiteSpace(c.FamilyId),
                DemographicFamilyId = c.FamilyId ?? "",
                DemographicFamilyRole = c.FamilyRole ?? "",
                DemographicRelationshipToHead = c.RelationshipToHead ?? "",
                IsDemographicHeadOfFamily = c.IsHouseholdHead,
                LinkStatus = "Unlinked",
                CedulaNo = c.CedulaNo,
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
