using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MySqlConnector;
using eSureHi.Data;
using eSureHi.Models;

namespace eSureHi.Services
{
    public enum SyncDirection
    {
        OnlineToOffline,
        OfflineToOnline,
        TwoWay
    }

    public class SyncResult
    {
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; } // ★ FIX: track per-entity failures instead of aborting whole sync
        public string? Details { get; set; }
        public string Message =>
            !string.IsNullOrWhiteSpace(Details)
                ? Details
                : $"Inserted: {Inserted}, Updated: {Updated}, Skipped older conflicts: {Skipped}, Failed: {Failed}";

        public void Add(SyncResult other)
        {
            Inserted += other.Inserted;
            Updated += other.Updated;
            Skipped += other.Skipped;
            Failed += other.Failed; // ★ FIX
            if (!string.IsNullOrWhiteSpace(other.Details))
                Details = string.IsNullOrWhiteSpace(Details)
                    ? other.Details
                    : $"{Details}; {other.Details}";
        }
    }

    public sealed class SyncStatusSnapshot
    {
        public bool IsOnline { get; init; }
        public bool IsSyncing { get; init; }
        public bool CrsUnavailable { get; init; }
        public bool GgmsUnavailable { get; init; }
        public DateTime CheckedAt { get; init; } = DateTime.Now;
        public string Message { get; init; } = "Checking sync...";
        public string Color { get; init; } = "#2563EB";
        public string CrossSystemMessage
        {
            get
            {
                var parts = new List<string>();
                if (CrsUnavailable) parts.Add("CRS unavailable");
                if (GgmsUnavailable) parts.Add("GGMS unavailable");
                return string.Join("  |  ", parts);
            }
        }
    }

    public static class OfflineOnlineSyncService
    {
        private static readonly SemaphoreSlim SyncLock = new(1, 1);
        private static Timer? _timer;
        private static SyncStatusSnapshot _current = new()
        {
            IsOnline = false,
            Message = "Offline",
            Color = "#F59E0B"
        };

        public static event Action<SyncStatusSnapshot>? StatusChanged;

        public static SyncStatusSnapshot CurrentStatus => _current;

        public static void StartBackgroundSync(TimeSpan? interval = null)
        {
            _timer?.Dispose();
            var syncInterval = interval ?? TimeSpan.FromMinutes(5);
            _timer = new Timer(
                async _ => await SyncAsync(SyncDirection.TwoWay),
                null,
                syncInterval,
                syncInterval);
        }

        public static void StopBackgroundSync()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public static async Task<(bool OnlineOk, bool OfflineOk, string Message)> TestConnectionsAsync()
        {
            var localOk = await CanOpenLocalAsync();
            var onlineOk = await CanConnectAsync(App.DbConfig);
            var message = $"Local SQLite ims.db: {(localOk ? "ready" : "not available")}; " +
                          $"Hostinger IMS cloud: {(onlineOk ? "connected" : "offline")}.";
            return (onlineOk, localOk, message);
        }

        public static async Task<SyncStatusSnapshot> CheckStatusAsync()
        {
            var connectResult = await CanConnectWithReasonAsync(App.DbConfig);
            var online = connectResult.success;
            var crsUnavailable = online && !await CanConnectSharedAsync(SharedDatabaseConfiguration.LoadCrs());
            var ggmsUnavailable = online && !await CanConnectSharedAsync(SharedDatabaseConfiguration.LoadGgms());

            var status = online
                ? new SyncStatusSnapshot
                {
                    IsOnline = true,
                    CrsUnavailable = crsUnavailable,
                    GgmsUnavailable = ggmsUnavailable,
                    Message = "Online - Synced",
                    Color = "#16A34A"
                }
                : new SyncStatusSnapshot
                {
                    IsOnline = false,
                    Message = string.IsNullOrWhiteSpace(connectResult.error) ? "Offline" : $"Offline ({connectResult.error})",
                    Color = "#F59E0B"
                };

            SetStatus(status);
            return status;
        }

        private static async Task<(bool success, string error)> CanConnectWithReasonAsync(DatabaseConfiguration config)
        {
            try
            {
                if (!config.IsConfigured)
                    return (false, "Not configured");

                var builder = new MySqlConnectionStringBuilder(config.ToConnectionString())
                {
                    ConnectionTimeout = 3
                };

                await using var conn = new MySqlConnection(builder.ConnectionString);
                await conn.OpenAsync();
                return (true, "");
            }
            catch (Exception ex)
            {
                try
                {
                    var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_error.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now}] CanConnectWithReasonAsync failed: {ex}\n\n");
                }
                catch { }

                var fullStr = ex.ToString();
                var baseMsg = ex.GetBaseException().Message;
                if (fullStr.Contains("MySqlRetryingExecutionStrategy", StringComparison.OrdinalIgnoreCase) ||
                    fullStr.Contains("maximum number of retries", StringComparison.OrdinalIgnoreCase) ||
                    fullStr.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase) ||
                    fullStr.Contains("Unable to connect", StringComparison.OrdinalIgnoreCase) ||
                    fullStr.Contains("Connection refused", StringComparison.OrdinalIgnoreCase) ||
                    fullStr.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
                    baseMsg.Contains("Unable to connect", StringComparison.OrdinalIgnoreCase) ||
                    baseMsg.Contains("Connection refused", StringComparison.OrdinalIgnoreCase) ||
                    baseMsg.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "Cloud host unreachable");
                }
                return (false, string.IsNullOrWhiteSpace(baseMsg) ? "Cloud host unreachable" : baseMsg);
            }
        }

        // Sync ownership/direction contract:
        //   • Local SQLite (ims.db) is the WORKING COPY used while offline — all data entry happens here first.
        //   • MySQL cloud (Hostinger) is the SHARED / AUTHORITATIVE store when online.
        // A TwoWay sync copies cloud→local first (so newer authoritative rows win) then local→cloud
        // (to push offline-entered rows up). Per-row conflicts are resolved by the getTimestamp selector
        // in UpsertAsync (newest UpdatedAt/CreatedAt wins); rows are matched by SyncId, falling back to a
        // natural key (emp_id / BeneficiaryId / CivilRegistryId) via FindExistingByNaturalKeyAsync before
        // inserting, so duplicate natural keys update in place instead of throwing UNIQUE violations.
        public static async Task<SyncResult> SyncAsync(SyncDirection direction)
        {
            if (!await SyncLock.WaitAsync(0))
                return new SyncResult { Details = "Sync already running." };

            try
            {
                SetStatus(new SyncStatusSnapshot
                {
                    IsSyncing = true,
                    Message = "Syncing...",
                    Color = "#2563EB"
                });

                await LocalDatabaseInitializer.InitializeAsync();

                var connectResult = await CanConnectWithReasonAsync(App.DbConfig);
                if (!connectResult.success)
                {
                    SetStatus(new SyncStatusSnapshot
                    {
                        IsOnline = false,
                        Message = string.IsNullOrWhiteSpace(connectResult.error) ? "Offline" : $"Offline ({connectResult.error})",
                        Color = "#F59E0B"
                    });
                    return new SyncResult { Details = "Changes are saved locally and will sync when Hostinger is available." };
                }

                await EnsureCloudSyncColumnsAsync();

                await using var local = eSureHiDbContextFactory.CreateLocal();
                await using var cloud = eSureHiDbContextFactory.CreateCloud();

                var result = new SyncResult();
                if (direction is SyncDirection.OnlineToOffline or SyncDirection.TwoWay)
                    result.Add(await CopyAsync(cloud, local));

                if (direction is SyncDirection.OfflineToOnline or SyncDirection.TwoWay)
                    result.Add(await CopyAsync(local, cloud));

                // Process/Sync local GGMS transaction queue
                await GgmsService.SyncQueueAsync();

                var crsUnavailable = !await CanConnectSharedAsync(SharedDatabaseConfiguration.LoadCrs());
                var ggmsUnavailable = !await CanConnectSharedAsync(SharedDatabaseConfiguration.LoadGgms());

                // ★ FIX: if some entities failed, still report "Online" so future syncs keep retrying,
                // but surface the failure count instead of silently looking fully synced.
                SetStatus(new SyncStatusSnapshot
                {
                    IsOnline = true,
                    CrsUnavailable = crsUnavailable,
                    GgmsUnavailable = ggmsUnavailable,
                    Message = result.Failed > 0
                        ? $"Online - Synced with {result.Failed} issue(s)"
                        : "Online - Synced",
                    Color = result.Failed > 0 ? "#F59E0B" : "#16A34A"
                });

                return result;
            }
            catch (Exception ex)
            {
                try
                {
                    var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_error.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now}] SyncAsync exception: {ex}\n\n");
                }
                catch { }
                SetStatus(new SyncStatusSnapshot
                {
                    IsOnline = false,
                    Message = "Offline",
                    Color = "#F59E0B"
                });
                return new SyncResult { Details = $"Sync failed: {ex.GetBaseException().Message}" };
            }
            finally
            {
                SyncLock.Release();
            }
        }

        private static async Task<bool> CanOpenLocalAsync()
        {
            try
            {
                await using var db = eSureHiDbContextFactory.CreateLocal();
                return await db.Database.CanConnectAsync();
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> CanConnectAsync(DatabaseConfiguration config)
        {
            try
            {
                if (!config.IsConfigured)
                    return false;

                var builder = new MySqlConnectionStringBuilder(config.ToConnectionString())
                {
                    ConnectionTimeout = 3
                };
                await using var conn = new MySqlConnection(builder.ConnectionString);
                await conn.OpenAsync();
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_error.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now}] CanConnectAsync failed: {ex}\n\n");
                }
                catch { }
                return false;
            }
        }

        private static async Task<bool> CanConnectSharedAsync(SharedDatabaseConfiguration config)
        {
            try
            {
                if (!config.IsConfigured)
                    return false;

                var builder = new MySqlConnectionStringBuilder(config.ToConnectionString())
                {
                    ConnectionTimeout = 3
                };
                await using var conn = new MySqlConnection(builder.ConnectionString);
                await conn.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool _cloudSyncColumnsEnsured = false;

        private static async Task EnsureCloudSyncColumnsAsync()
        {
            if (_cloudSyncColumnsEnsured)
                return;

            try
            {
                var builder = new MySqlConnectionStringBuilder(App.DbConfig.ToConnectionString())
                {
                    ConnectionTimeout = 5
                };
                await using var conn = new MySqlConnection(builder.ConnectionString);
                await conn.OpenAsync();

                // 1. Fetch all existing tables and their columns in a single fast query
                var existingTableColumns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT TABLE_NAME, COLUMN_NAME
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_SCHEMA = DATABASE();";
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var tbl = reader.GetString(0);
                        var col = reader.GetString(1);
                        if (!existingTableColumns.TryGetValue(tbl, out var cols))
                        {
                            cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            existingTableColumns[tbl] = cols;
                        }
                        cols.Add(col);
                    }
                }

                await using var modelDb = eSureHiDbContextFactory.CreateLocal();
                foreach (var clrType in eSureHiDbContext.SyncEntityTypes)
                {
                    var entityType = modelDb.Model.FindEntityType(clrType);
                    var tableName = entityType?.GetTableName();
                    if (string.IsNullOrWhiteSpace(tableName))
                        continue;

                    if (!existingTableColumns.TryGetValue(tableName, out var cols))
                        continue;

                    if (!cols.Contains("SyncId"))
                    {
                        try
                        {
                            await using var add = conn.CreateCommand();
                            add.CommandText = $"ALTER TABLE `{tableName}` ADD COLUMN `SyncId` VARCHAR(36) NULL;";
                            await add.ExecuteNonQueryAsync();
                        }
                        catch { }

                        try
                        {
                            await using var fill = conn.CreateCommand();
                            fill.CommandText = $"UPDATE `{tableName}` SET `SyncId` = UUID() WHERE `SyncId` IS NULL OR `SyncId` = '';";
                            await fill.ExecuteNonQueryAsync();
                        }
                        catch { }

                        try
                        {
                            await using var modify = conn.CreateCommand();
                            modify.CommandText = $"ALTER TABLE `{tableName}` MODIFY COLUMN `SyncId` VARCHAR(36) NOT NULL;";
                            await modify.ExecuteNonQueryAsync();
                        }
                        catch { }

                        try
                        {
                            await using var index = conn.CreateCommand();
                            index.CommandText = $"CREATE UNIQUE INDEX `ux_{tableName}_syncid` ON `{tableName}` (`SyncId`);";
                            await index.ExecuteNonQueryAsync();
                        }
                        catch { }
                    }
                }

                _cloudSyncColumnsEnsured = true;
            }
            catch (Exception ex)
            {
                try
                {
                    var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_error.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now}] EnsureCloudSyncColumnsAsync exception: {ex}\n\n");
                }
                catch { }
            }
        }

        private static async Task<SyncResult> CopyAsync(eSureHiDbContext source, eSureHiDbContext target)
        {
            var result = new SyncResult();

            // SafeUpsertAsync encapsulates source fetching + upserting per-table to isolate failures
            result.Add(await SafeUpsertAsync("Departments", source, db => db.Departments.ToListAsync(), target, target.Departments, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("Employees", source, db => db.Employees.ToListAsync(), target, target.Employees, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("SystemUsers", source, db => db.SystemUsers.ToListAsync(), target, target.SystemUsers, x => x.LastLogin ?? x.CreatedAt));
            result.Add(await SafeUpsertAsync("UserPermissions", source, db => db.UserPermissions.ToListAsync(), target, target.UserPermissions, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("InsurancePolicies", source, db => db.InsurancePolicies.ToListAsync(), target, target.InsurancePolicies, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("CompanyProfiles", source, db => db.CompanyProfiles.ToListAsync(), target, target.CompanyProfiles, _ => null));
            result.Add(await SafeUpsertAsync("EmployeePolicies", source, db => db.EmployeePolicies.ToListAsync(), target, target.EmployeePolicies, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("Beneficiaries", source, db => db.Beneficiaries.ToListAsync(), target, target.Beneficiaries, x => x.CreatedAt));
            result.Add(await SafeUpsertAsync("BeneficiaryStaging", source, db => db.BeneficiaryStaging.ToListAsync(), target, target.BeneficiaryStaging, x => x.ImportedAt));
            result.Add(await SafeUpsertAsync("ResidentDemographics", source, db => db.ResidentDemographics.ToListAsync(), target, target.ResidentDemographics, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("Contributions", source, db => db.Contributions.ToListAsync(), target, target.Contributions, x => x.CreatedAt));
            result.Add(await SafeUpsertAsync("Trainings", source, db => db.Trainings.ToListAsync(), target, target.Trainings, _ => null));
            result.Add(await SafeUpsertAsync("Cedulas", source, db => db.Cedulas.ToListAsync(), target, target.Cedulas, x => x.CreatedAt));
            result.Add(await SafeUpsertAsync("Claims", source, db => db.Claims.ToListAsync(), target, target.Claims, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("ClaimDocuments", source, db => db.ClaimDocuments.ToListAsync(), target, target.ClaimDocuments, x => x.UploadedAt));
            result.Add(await SafeUpsertAsync("Benefits", source, db => db.Benefits.ToListAsync(), target, target.Benefits, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("Premiums", source, db => db.Premiums.ToListAsync(), target, target.Premiums, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("DocumentTypes", source, db => db.DocumentTypes.ToListAsync(), target, target.DocumentTypes, x => x.CreatedAt));
            result.Add(await SafeUpsertAsync("Documents", source, db => db.Documents.ToListAsync(), target, target.Documents, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("Senders", source, db => db.Senders.ToListAsync(), target, target.Senders, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("Receivers", source, db => db.Receivers.ToListAsync(), target, target.Receivers, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("DocumentTransactions", source, db => db.DocumentTransactions.ToListAsync(), target, target.DocumentTransactions, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("Notifications", source, db => db.Notifications.ToListAsync(), target, target.Notifications, x => x.CreatedAt));
            result.Add(await SafeUpsertAsync("AuditLogs", source, db => db.AuditLogs.ToListAsync(), target, target.AuditLogs, x => x.LoggedAt));
            result.Add(await SafeUpsertAsync("SourceFunds", source, db => db.SourceFunds.ToListAsync(), target, target.SourceFunds, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("DistributionBatches", source, db => db.DistributionBatches.ToListAsync(), target, target.DistributionBatches, x => x.CreatedAt));
            result.Add(await SafeUpsertAsync("DistributionRecords", source, db => db.DistributionRecords.ToListAsync(), target, target.DistributionRecords, x => x.ProcessedAt));
            result.Add(await SafeUpsertAsync("GgmsQueueItems", source, db => db.GgmsQueueItems.ToListAsync(), target, target.GgmsQueueItems, x => x.UpdatedAt));

            source.ChangeTracker.Clear();
            return result;
        }

        private static async Task<SyncResult> SafeUpsertAsync<T>(
            string entityLabel,
            eSureHiDbContext source,
            Func<eSureHiDbContext, Task<List<T>>> sourceFetcher,
            eSureHiDbContext target,
            DbSet<T> targetSet,
            Func<T, DateTime?> getTimestamp) where T : class
        {
            try
            {
                var sourceRows = await sourceFetcher(source);
                return await UpsertAsync(source, sourceRows, target, targetSet, getTimestamp);
            }
            catch (Exception ex)
            {
                target.ChangeTracker.Clear();
                source.ChangeTracker.Clear();
                try
                {
                    var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync_error.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now}] SafeUpsertAsync '{entityLabel}' exception: {ex}\n\n");
                }
                catch { }

                return new SyncResult
                {
                    Failed = 1,
                    Details = $"{entityLabel} sync skipped: {ex.GetBaseException().Message}"
                };
            }
        }

        private static async Task<SyncResult> UpsertAsync<T>(
            eSureHiDbContext source,
            List<T> sourceRows,
            eSureHiDbContext target,
            DbSet<T> targetSet,
            Func<T, DateTime?> getTimestamp) where T : class
        {
            var result = new SyncResult();
            if (sourceRows == null || sourceRows.Count == 0)
                return result;

            // Load all target rows once so we can perform lookups in memory (0 network round-trips)
            var targetRows = await targetSet.ToListAsync();

            // Index existing target rows by SyncId
            var targetBySyncId = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
            foreach (var tr in targetRows)
            {
                var sId = target.Entry(tr).Property<string>("SyncId").CurrentValue;
                if (!string.IsNullOrWhiteSpace(sId) && !targetBySyncId.ContainsKey(sId))
                {
                    targetBySyncId[sId] = tr;
                }
            }

            var entityType = target.Model.FindEntityType(typeof(T));
            var candidateKeyGroups = entityType?.GetKeys()
                .Where(k => !k.IsPrimaryKey())
                .Select(k => k.Properties.Select(p => p.Name).ToArray())
                .Concat(entityType.GetIndexes()
                    .Where(i => i.IsUnique)
                    .Select(i => i.Properties.Select(p => p.Name).ToArray()))
                .Where(names => names.Length > 0 && !(names.Length == 1 && names[0] == "SyncId"))
                .ToList();

            foreach (var row in sourceRows)
            {
                var syncId = source.Entry(row).Property<string>("SyncId").CurrentValue;
                if (string.IsNullOrWhiteSpace(syncId))
                {
                    syncId = Guid.NewGuid().ToString();
                    source.Entry(row).Property("SyncId").CurrentValue = syncId;
                }

                T? existing = null;
                if (targetBySyncId.TryGetValue(syncId, out var directMatch))
                {
                    existing = directMatch;
                }
                else
                {
                    existing = FindExistingInTargetMemory(source, row, target, targetRows, candidateKeyGroups);
                }

                if (existing is null)
                {
                    var clone = Activator.CreateInstance<T>();
                    CopyValues(source, row, target, clone, includeKeys: false);
                    target.Entry(clone).Property("SyncId").CurrentValue = syncId;
                    targetSet.Add(clone);
                    targetBySyncId[syncId] = clone;
                    result.Inserted++;
                    continue;
                }

                var sourceTime = getTimestamp(row);
                var targetTime = getTimestamp(existing);
                if (sourceTime.HasValue && targetTime.HasValue && targetTime.Value > sourceTime.Value)
                {
                    result.Skipped++;
                    continue;
                }

                CopyValues(source, row, target, existing, includeKeys: false);
                target.Entry(existing).Property("SyncId").CurrentValue = syncId;
                targetBySyncId[syncId] = existing;
                result.Updated++;
            }

            if (source.ChangeTracker.HasChanges())
                await source.SaveChangesAsync();

            if (result.Inserted > 0 || result.Updated > 0)
                await target.SaveChangesAsync();

            target.ChangeTracker.Clear();
            source.ChangeTracker.Clear();
            return result;
        }

        private static T? FindExistingInTargetMemory<T>(
            eSureHiDbContext sourceContext,
            T sourceRow,
            eSureHiDbContext targetContext,
            List<T> targetRows,
            List<string[]>? candidateKeyGroups) where T : class
        {
            var sourceEntry = sourceContext.Entry(sourceRow);
            var entityType = sourceEntry.Metadata;

            // 1. Match by Primary Key (if PK property values are non-default)
            var pkProps = entityType.FindPrimaryKey()?.Properties;
            if (pkProps != null && pkProps.Count > 0)
            {
                var pkValues = pkProps.Select(p => sourceEntry.Property(p.Name).CurrentValue).ToArray();
                bool hasValidPk = pkValues.All(v => v != null && !IsDefaultValue(v));
                if (hasValidPk)
                {
                    foreach (var candidate in targetRows)
                    {
                        var candidateEntry = targetContext.Entry(candidate);
                        bool match = true;
                        for (int i = 0; i < pkProps.Count; i++)
                        {
                            var targetVal = candidateEntry.Property(pkProps[i].Name).CurrentValue;
                            if (!Equals(pkValues[i], targetVal))
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match) return candidate;
                    }
                }
            }

            // 2. Match by Candidate Key Groups / Unique Indexes
            if (candidateKeyGroups != null && candidateKeyGroups.Count > 0)
            {
                var match = FindExistingByNaturalKeyInMemory(sourceContext, sourceRow, targetContext, targetRows, candidateKeyGroups);
                if (match != null) return match;
            }

            // 3. Fallback: Domain-specific Natural Key matching
            return FindBySpecificNaturalKey(sourceRow, targetContext, targetRows);
        }

        private static bool IsDefaultValue(object? val)
        {
            if (val is null) return true;
            if (val is int i && i == 0) return true;
            if (val is long l && l == 0L) return true;
            if (val is string s && string.IsNullOrWhiteSpace(s)) return true;
            if (val is Guid g && g == Guid.Empty) return true;
            return false;
        }

        private static T? FindBySpecificNaturalKey<T>(
            T sourceRow,
            eSureHiDbContext targetContext,
            List<T> targetRows) where T : class
        {
            if (sourceRow is Employee emp)
            {
                foreach (var candidate in targetRows.OfType<Employee>())
                {
                    if (!string.IsNullOrWhiteSpace(emp.EmployeeNo) &&
                        string.Equals(emp.EmployeeNo, candidate.EmployeeNo, StringComparison.OrdinalIgnoreCase))
                        return candidate as T;

                    if (!string.IsNullOrWhiteSpace(emp.Email) &&
                        string.Equals(emp.Email, candidate.Email, StringComparison.OrdinalIgnoreCase))
                        return candidate as T;

                    if (!string.IsNullOrWhiteSpace(emp.FirstName) && !string.IsNullOrWhiteSpace(emp.LastName) &&
                        string.Equals(emp.FirstName, candidate.FirstName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(emp.LastName, candidate.LastName, StringComparison.OrdinalIgnoreCase) &&
                        emp.DateOfBirth == candidate.DateOfBirth)
                        return candidate as T;
                }
            }
            else if (sourceRow is Department dept)
            {
                foreach (var candidate in targetRows.OfType<Department>())
                {
                    if (!string.IsNullOrWhiteSpace(dept.DeptCode) &&
                        string.Equals(dept.DeptCode, candidate.DeptCode, StringComparison.OrdinalIgnoreCase))
                        return candidate as T;

                    if (!string.IsNullOrWhiteSpace(dept.DeptName) &&
                        string.Equals(dept.DeptName, candidate.DeptName, StringComparison.OrdinalIgnoreCase))
                        return candidate as T;
                }
            }
            else if (sourceRow is SystemUser user)
            {
                foreach (var candidate in targetRows.OfType<SystemUser>())
                {
                    if (!string.IsNullOrWhiteSpace(user.Username) &&
                        string.Equals(user.Username, candidate.Username, StringComparison.OrdinalIgnoreCase))
                        return candidate as T;
                }
            }
            else if (sourceRow is InsurancePolicy policy)
            {
                foreach (var candidate in targetRows.OfType<InsurancePolicy>())
                {
                    if (!string.IsNullOrWhiteSpace(policy.PolicyCode) &&
                        string.Equals(policy.PolicyCode, candidate.PolicyCode, StringComparison.OrdinalIgnoreCase))
                        return candidate as T;

                    if (!string.IsNullOrWhiteSpace(policy.PolicyName) &&
                        string.Equals(policy.PolicyName, candidate.PolicyName, StringComparison.OrdinalIgnoreCase))
                        return candidate as T;
                }
            }
            else if (sourceRow is Claim claim)
            {
                foreach (var candidate in targetRows.OfType<Claim>())
                {
                    if (!string.IsNullOrWhiteSpace(claim.ClaimNo) &&
                        string.Equals(claim.ClaimNo, candidate.ClaimNo, StringComparison.OrdinalIgnoreCase))
                        return candidate as T;
                }
            }
            else if (sourceRow is Beneficiary ben)
            {
                foreach (var candidate in targetRows.OfType<Beneficiary>())
                {
                    if (!string.IsNullOrWhiteSpace(ben.FirstName) && !string.IsNullOrWhiteSpace(ben.LastName) &&
                        string.Equals(ben.FirstName, candidate.FirstName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(ben.LastName, candidate.LastName, StringComparison.OrdinalIgnoreCase) &&
                        ben.DateOfBirth == candidate.DateOfBirth)
                        return candidate as T;
                }
            }
            else if (sourceRow is Cedula ced)
            {
                foreach (var candidate in targetRows.OfType<Cedula>())
                {
                    if (!string.IsNullOrWhiteSpace(ced.CedulaNo) &&
                        string.Equals(ced.CedulaNo, candidate.CedulaNo, StringComparison.OrdinalIgnoreCase))
                        return candidate as T;
                }
            }
            else if (sourceRow is Document doc)
            {
                foreach (var candidate in targetRows.OfType<Document>())
                {
                    if (!string.IsNullOrWhiteSpace(doc.DocTitle) && !string.IsNullOrWhiteSpace(doc.FileName) &&
                        string.Equals(doc.DocTitle, candidate.DocTitle, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(doc.FileName, candidate.FileName, StringComparison.OrdinalIgnoreCase))
                        return candidate as T;
                }
            }
            else if (sourceRow is CompanyProfile comp)
            {
                foreach (var candidate in targetRows.OfType<CompanyProfile>())
                {
                    if (!string.IsNullOrWhiteSpace(comp.Name) &&
                        string.Equals(comp.Name, candidate.Name, StringComparison.OrdinalIgnoreCase))
                        return candidate as T;
                }
            }

            return null;
        }

        private static T? FindExistingByNaturalKeyInMemory<T>(
            eSureHiDbContext sourceContext,
            T sourceRow,
            eSureHiDbContext targetContext,
            List<T> targetRows,
            List<string[]> candidateKeyGroups) where T : class
        {
            var sourceEntry = sourceContext.Entry(sourceRow);

            foreach (var propNames in candidateKeyGroups)
            {
                object?[] values;
                try
                {
                    values = propNames
                        .Select(name => sourceEntry.Property(name).CurrentValue)
                        .ToArray();
                }
                catch
                {
                    continue; // property not tracked/found on this entity, skip this key group
                }

                if (values.Any(v => v is null))
                    continue; // avoid matching everything on an all-null key

                foreach (var candidate in targetRows)
                {
                    var candidateEntry = targetContext.Entry(candidate);
                    bool match = true;
                    for (int i = 0; i < propNames.Length; i++)
                    {
                        object? targetVal;
                        try
                        {
                            targetVal = candidateEntry.Property(propNames[i]).CurrentValue;
                        }
                        catch
                        {
                            match = false;
                            break;
                        }

                        if (!Equals(values[i], targetVal))
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                        return candidate;
                }
            }

            return null;
        }

        private static void CopyValues<T>(
            eSureHiDbContext sourceContext,
            T source,
            eSureHiDbContext targetContext,
            T target,
            bool includeKeys) where T : class
        {
            var sourceEntry = sourceContext.Entry(source);
            var targetEntry = targetContext.Entry(target);

            foreach (var property in targetEntry.Properties)
            {
                if (!includeKeys && property.Metadata.IsPrimaryKey())
                    continue;

                // Never overwrite store-generated/computed columns
                if (property.Metadata.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate)
                    continue;

                var sourceProperty = sourceEntry.Property(property.Metadata.Name);
                property.CurrentValue = sourceProperty.CurrentValue;
            }
        }

        private static void SetStatus(SyncStatusSnapshot status)
        {
            _current = status;
            StatusChanged?.Invoke(status);
        }
    }
}
