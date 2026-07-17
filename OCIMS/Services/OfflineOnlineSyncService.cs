using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
        public string? Details { get; set; }
        public string Message =>
            !string.IsNullOrWhiteSpace(Details)
                ? Details
                : $"Inserted: {Inserted}, Updated: {Updated}, Skipped older conflicts: {Skipped}";

        public void Add(SyncResult other)
        {
            Inserted += other.Inserted;
            Updated += other.Updated;
            Skipped += other.Skipped;
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
                SetStatus(new SyncStatusSnapshot
                {
                    IsOnline = true,
                    CrsUnavailable = crsUnavailable,
                    GgmsUnavailable = ggmsUnavailable,
                    Message = "Online - Synced",
                    Color = "#16A34A"
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

            result.Add(await UpsertAsync(source, await source.Departments.ToListAsync(), target, target.Departments, x => x.UpdatedAt));
            result.Add(await UpsertAsync(source, await source.Employees.ToListAsync(), target, target.Employees, x => x.UpdatedAt));
            result.Add(await UpsertAsync(source, await source.SystemUsers.ToListAsync(), target, target.SystemUsers, x => x.LastLogin ?? x.CreatedAt));
            result.Add(await UpsertAsync(source, await source.UserPermissions.ToListAsync(), target, target.UserPermissions, x => x.UpdatedAt));
            result.Add(await UpsertAsync(source, await source.InsurancePolicies.ToListAsync(), target, target.InsurancePolicies, x => x.UpdatedAt));
            result.Add(await UpsertAsync(source, await source.CompanyProfiles.ToListAsync(), target, target.CompanyProfiles, _ => null));
            result.Add(await UpsertAsync(source, await source.EmployeePolicies.ToListAsync(), target, target.EmployeePolicies, x => x.UpdatedAt));
            result.Add(await UpsertAsync(source, await source.Beneficiaries.ToListAsync(), target, target.Beneficiaries, x => x.CreatedAt));
            result.Add(await UpsertAsync(source, await source.BeneficiaryStaging.ToListAsync(), target, target.BeneficiaryStaging, x => x.ImportedAt));
            result.Add(await UpsertAsync(source, await source.ResidentDemographics.ToListAsync(), target, target.ResidentDemographics, x => x.UpdatedAt));
            result.Add(await UpsertAsync(source, await source.Contributions.ToListAsync(), target, target.Contributions, x => x.CreatedAt));
            result.Add(await UpsertAsync(source, await source.Trainings.ToListAsync(), target, target.Trainings, _ => null));
            result.Add(await UpsertAsync(source, await source.Cedulas.ToListAsync(), target, target.Cedulas, x => x.CreatedAt));
            result.Add(await UpsertAsync(source, await source.Claims.ToListAsync(), target, target.Claims, x => x.UpdatedAt));
            result.Add(await UpsertAsync(source, await source.ClaimDocuments.ToListAsync(), target, target.ClaimDocuments, x => x.UploadedAt));
            result.Add(await UpsertAsync(source, await source.Benefits.ToListAsync(), target, target.Benefits, x => x.UpdatedAt));
            result.Add(await UpsertAsync(source, await source.Premiums.ToListAsync(), target, target.Premiums, x => x.UpdatedAt));
            result.Add(await UpsertAsync(source, await source.DocumentTypes.ToListAsync(), target, target.DocumentTypes, x => x.CreatedAt));
            result.Add(await UpsertAsync(source, await source.Documents.ToListAsync(), target, target.Documents, x => x.UpdatedAt));
            result.Add(await UpsertAsync(source, await source.Senders.ToListAsync(), target, target.Senders, x => x.UpdatedAt));
            result.Add(await UpsertAsync(source, await source.Receivers.ToListAsync(), target, target.Receivers, x => x.UpdatedAt));
            result.Add(await UpsertAsync(source, await source.DocumentTransactions.ToListAsync(), target, target.DocumentTransactions, x => x.UpdatedAt));
            result.Add(await UpsertAsync(source, await source.Notifications.ToListAsync(), target, target.Notifications, x => x.CreatedAt));
            result.Add(await UpsertAsync(source, await source.AuditLogs.ToListAsync(), target, target.AuditLogs, x => x.LoggedAt));
            result.Add(await UpsertAsync(source, await source.SourceFunds.ToListAsync(), target, target.SourceFunds, x => x.UpdatedAt));

            source.ChangeTracker.Clear();
            return result;
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
