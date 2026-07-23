using System;
using System.Collections.Generic;
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

                await using var db = eSureHiDbContextFactory.CreateCloud(config);
                await db.Database.OpenConnectionAsync();
                await db.Database.CloseConnectionAsync();
                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
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

                if (!await CanConnectAsync(App.DbConfig))
                {
                    SetStatus(new SyncStatusSnapshot
                    {
                        IsOnline = false,
                        Message = "Offline",
                        Color = "#F59E0B"
                    });
                    return new SyncResult { Details = "Offline. Changes are saved locally and will sync when Hostinger is available." };
                }

                await EnsureCloudSyncColumnsAsync();

                await using var local = eSureHiDbContextFactory.Create();
                await using var cloud = eSureHiDbContextFactory.CreateCloud();

                var result = new SyncResult();
                if (direction is SyncDirection.OnlineToOffline or SyncDirection.TwoWay)
                    result.Add(await CopyAsync(cloud, local));

                if (direction is SyncDirection.OfflineToOnline or SyncDirection.TwoWay)
                    result.Add(await CopyAsync(local, cloud));

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
                await using var db = eSureHiDbContextFactory.Create();
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

                await using var db = eSureHiDbContextFactory.CreateCloud(config);
                return await db.Database.CanConnectAsync();
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> CanConnectSharedAsync(SharedDatabaseConfiguration config)
        {
            try
            {
                if (!config.IsConfigured)
                    return false;

                await using var conn = new MySqlConnection(config.ToConnectionString());
                await conn.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static async Task EnsureCloudSyncColumnsAsync()
        {
            await using var conn = new MySqlConnection(App.DbConfig.ToConnectionString());
            await conn.OpenAsync();

            await using var modelDb = eSureHiDbContextFactory.Create();
            foreach (var clrType in eSureHiDbContext.SyncEntityTypes)
            {
                var entityType = modelDb.Model.FindEntityType(clrType);
                var tableName = entityType?.GetTableName();
                if (string.IsNullOrWhiteSpace(tableName))
                    continue;

                if (!await CloudTableExistsAsync(conn, tableName))
                    continue;

                if (!await CloudColumnExistsAsync(conn, tableName, "SyncId"))
                {
                    await using var add = conn.CreateCommand();
                    add.CommandText = $"ALTER TABLE `{tableName}` ADD COLUMN `SyncId` VARCHAR(36) NULL;";
                    await add.ExecuteNonQueryAsync();
                }

                await using (var fill = conn.CreateCommand())
                {
                    fill.CommandText = $"UPDATE `{tableName}` SET `SyncId` = UUID() WHERE `SyncId` IS NULL OR `SyncId` = '';";
                    await fill.ExecuteNonQueryAsync();
                }

                try
                {
                    await using var modify = conn.CreateCommand();
                    modify.CommandText = $"ALTER TABLE `{tableName}` MODIFY COLUMN `SyncId` VARCHAR(36) NOT NULL;";
                    await modify.ExecuteNonQueryAsync();
                }
                catch
                {
                    // Some shared-hosting users may not have ALTER MODIFY permission.
                }

                try
                {
                    await using var index = conn.CreateCommand();
                    index.CommandText = $"CREATE UNIQUE INDEX `ux_{tableName}_syncid` ON `{tableName}` (`SyncId`);";
                    await index.ExecuteNonQueryAsync();
                }
                catch
                {
                    // Existing indexes and duplicate legacy data are handled by SyncId matching.
                }
            }
        }

        private static async Task<bool> CloudTableExistsAsync(MySqlConnection conn, string tableName)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @table_name;";
            cmd.Parameters.AddWithValue("@table_name", tableName);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        private static async Task<bool> CloudColumnExistsAsync(MySqlConnection conn, string tableName, string columnName)
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

        private static async Task<SyncResult> CopyAsync(eSureHiDbContext source, eSureHiDbContext target)
        {
            var result = new SyncResult();

            // ★ FIX: each entity's upsert now runs through SafeUpsertAsync, which catches
            // per-entity failures so one bad table (e.g. duplicate emp_id) can no longer
            // abort the sync of every other table (Beneficiaries, BeneficiaryStaging, etc.)
            result.Add(await SafeUpsertAsync("Departments", source, await source.Departments.ToListAsync(), target, target.Departments, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("Employees", source, await source.Employees.ToListAsync(), target, target.Employees, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("SystemUsers", source, await source.SystemUsers.ToListAsync(), target, target.SystemUsers, x => x.LastLogin ?? x.CreatedAt));
            result.Add(await SafeUpsertAsync("UserPermissions", source, await source.UserPermissions.ToListAsync(), target, target.UserPermissions, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("InsurancePolicies", source, await source.InsurancePolicies.ToListAsync(), target, target.InsurancePolicies, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("CompanyProfiles", source, await source.CompanyProfiles.ToListAsync(), target, target.CompanyProfiles, _ => null));
            result.Add(await SafeUpsertAsync("EmployeePolicies", source, await source.EmployeePolicies.ToListAsync(), target, target.EmployeePolicies, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("Beneficiaries", source, await source.Beneficiaries.ToListAsync(), target, target.Beneficiaries, x => x.CreatedAt));
            result.Add(await SafeUpsertAsync("BeneficiaryStaging", source, await source.BeneficiaryStaging.ToListAsync(), target, target.BeneficiaryStaging, x => x.ImportedAt));
            result.Add(await SafeUpsertAsync("ResidentDemographics", source, await source.ResidentDemographics.ToListAsync(), target, target.ResidentDemographics, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("Contributions", source, await source.Contributions.ToListAsync(), target, target.Contributions, x => x.CreatedAt));
            result.Add(await SafeUpsertAsync("Trainings", source, await source.Trainings.ToListAsync(), target, target.Trainings, _ => null));
            result.Add(await SafeUpsertAsync("Cedulas", source, await source.Cedulas.ToListAsync(), target, target.Cedulas, x => x.CreatedAt));
            result.Add(await SafeUpsertAsync("Claims", source, await source.Claims.ToListAsync(), target, target.Claims, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("ClaimDocuments", source, await source.ClaimDocuments.ToListAsync(), target, target.ClaimDocuments, x => x.UploadedAt));
            result.Add(await SafeUpsertAsync("Benefits", source, await source.Benefits.ToListAsync(), target, target.Benefits, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("Premiums", source, await source.Premiums.ToListAsync(), target, target.Premiums, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("DocumentTypes", source, await source.DocumentTypes.ToListAsync(), target, target.DocumentTypes, x => x.CreatedAt));
            result.Add(await SafeUpsertAsync("Documents", source, await source.Documents.ToListAsync(), target, target.Documents, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("Senders", source, await source.Senders.ToListAsync(), target, target.Senders, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("Receivers", source, await source.Receivers.ToListAsync(), target, target.Receivers, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("DocumentTransactions", source, await source.DocumentTransactions.ToListAsync(), target, target.DocumentTransactions, x => x.UpdatedAt));
            result.Add(await SafeUpsertAsync("Notifications", source, await source.Notifications.ToListAsync(), target, target.Notifications, x => x.CreatedAt));
            result.Add(await SafeUpsertAsync("AuditLogs", source, await source.AuditLogs.ToListAsync(), target, target.AuditLogs, x => x.LoggedAt));
            result.Add(await SafeUpsertAsync("SourceFunds", source, await source.SourceFunds.ToListAsync(), target, target.SourceFunds, x => x.UpdatedAt));

            source.ChangeTracker.Clear();
            return result;
        }

        // ★ FIX: new wrapper — isolates each entity type's sync so a failure (e.g. UNIQUE
        // constraint on employees.emp_id) is recorded and skipped instead of bubbling up
        // and killing the sync for every other table.
        private static async Task<SyncResult> SafeUpsertAsync<T>(
            string entityLabel,
            eSureHiDbContext source,
            List<T> sourceRows,
            eSureHiDbContext target,
            DbSet<T> targetSet,
            Func<T, DateTime?> getTimestamp) where T : class
        {
            try
            {
                return await UpsertAsync(source, sourceRows, target, targetSet, getTimestamp);
            }
            catch (Exception ex)
            {
                // Roll back whatever this entity type staged so it doesn't poison
                // the next entity's SaveChanges call.
                target.ChangeTracker.Clear();
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

            foreach (var row in sourceRows)
            {
                var syncId = source.Entry(row).Property<string>("SyncId").CurrentValue;
                if (string.IsNullOrWhiteSpace(syncId))
                {
                    syncId = Guid.NewGuid().ToString();
                    source.Entry(row).Property("SyncId").CurrentValue = syncId;
                }

                var existing = await targetSet
                    .FirstOrDefaultAsync(x => EF.Property<string>(x, "SyncId") == syncId);

                // ★ FIX: fall back to matching on the entity's natural/unique key
                // (e.g. Employees.EmpId) when SyncId doesn't match. Without this,
                // two independently-created rows with the same emp_id but different
                // SyncId look "new" to both sides and the insert throws a UNIQUE
                // constraint violation instead of merging into the existing row.
                existing ??= await FindExistingByNaturalKeyAsync(source, row, target, targetSet);

                if (existing is null)
                {
                    var clone = Activator.CreateInstance<T>();
                    targetSet.Add(clone);
                    CopyValues(source, row, target, clone, includeKeys: true);
                    target.Entry(clone).Property("SyncId").CurrentValue = syncId;
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
                result.Updated++;
            }

            if (source.ChangeTracker.HasChanges())
                await source.SaveChangesAsync();

            if (result.Inserted > 0 || result.Updated > 0)
                await target.SaveChangesAsync();

            target.ChangeTracker.Clear();
            return result;
        }

        // ★ FIX: new helper — looks up alternate keys / unique indexes (other than SyncId)
        // defined on the entity, and tries to find a matching row in target using those
        // values. This is what lets Employees.EmpId (and any other unique business key
        // on any synced table) reconcile correctly instead of duplicating.
        private static async Task<T?> FindExistingByNaturalKeyAsync<T>(
            eSureHiDbContext sourceContext,
            T sourceRow,
            eSureHiDbContext targetContext,
            DbSet<T> targetSet) where T : class
        {
            var entityType = targetContext.Model.FindEntityType(typeof(T));
            if (entityType is null)
                return null;

            var sourceEntry = sourceContext.Entry(sourceRow);

            var candidateKeyGroups = entityType.GetKeys()
                .Where(k => !k.IsPrimaryKey())
                .Select(k => k.Properties.Select(p => p.Name).ToArray())
                .Concat(entityType.GetIndexes()
                    .Where(i => i.IsUnique)
                    .Select(i => i.Properties.Select(p => p.Name).ToArray()))
                .Where(names => names.Length > 0 && !(names.Length == 1 && names[0] == "SyncId"));

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

                IQueryable<T> query = targetSet;
                for (int i = 0; i < propNames.Length; i++)
                {
                    var propName = propNames[i];
                    var value = values[i];
                    query = query.Where(e => EF.Property<object>(e, propName) == value);
                }

                var match = await query.FirstOrDefaultAsync();
                if (match is not null)
                    return match;
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