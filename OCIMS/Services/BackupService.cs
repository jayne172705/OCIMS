using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Models;

namespace eSureHi.Services
{
    public static class BackupService
    {
        // ── Directory structure ────────────────────────────────────────
        // <BackupRoot>/a
        //   backup_metadata.json
        //   full/         full_yyyyMMdd_HHmmss.json
        //   differential/ diff_yyyyMMdd_HHmmss.json
        //   incremental/  inc_yyyyMMdd_HHmmss.json

        private static string BackupRoot =>
            string.IsNullOrWhiteSpace(LoadMetadata().DefaultBackupPath)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups")
                : LoadMetadata().DefaultBackupPath!;

        private static string FullDir => Path.Combine(BackupRoot, "full");
        private static string DifferentialDir => Path.Combine(BackupRoot, "differential");
        private static string IncrementalDir => Path.Combine(BackupRoot, "incremental");

        private static string MetadataPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup_metadata.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // ── Metadata ───────────────────────────────────────────────────
        public static BackupMetadata LoadMetadata()
        {
            if (!File.Exists(MetadataPath)) return new BackupMetadata();
            try
            {
                return JsonSerializer.Deserialize<BackupMetadata>(
                    File.ReadAllText(MetadataPath)) ?? new BackupMetadata();
            }
            catch { return new BackupMetadata(); }
        }

        public static void SaveMetadata(BackupMetadata metadata)
        {
            File.WriteAllText(MetadataPath,
                JsonSerializer.Serialize(metadata, JsonOptions));
        }

        public static void SetDefaultBackupPath(string path)
        {
            var metadata = LoadMetadata();
            metadata.DefaultBackupPath = path;
            SaveMetadata(metadata);
        }

        private static void EnsureDirectories()
        {
            Directory.CreateDirectory(BackupRoot);
            Directory.CreateDirectory(FullDir);
            Directory.CreateDirectory(DifferentialDir);
            Directory.CreateDirectory(IncrementalDir);
        }

        public static BackupInfo GetBackupInfo()
        {
            var metadata = LoadMetadata();
            return new BackupInfo
            {
                Metadata = metadata,
                FullBackupCount = Directory.Exists(FullDir)
                    ? Directory.GetFiles(FullDir, "*.json").Length : 0,
                DifferentialBackupCount = Directory.Exists(DifferentialDir)
                    ? Directory.GetFiles(DifferentialDir, "*.json").Length : 0,
                IncrementalBackupCount = Directory.Exists(IncrementalDir)
                    ? Directory.GetFiles(IncrementalDir, "*.json").Length : 0
            };
        }

        // ── Full Backup ────────────────────────────────────────────────
        public static string ExecuteFullBackup(string? overridePath = null)
        {
            EnsureDirectories();
            var now = DateTime.Now;
            using var db = eSureHiDbContextFactory.Create();

            var data = new eSureHiBackupData
            {
                BackupType = "Full",
                CreatedAt = now,
                ReferenceDate = null,

                // Timestamped
                Departments = db.Departments.AsNoTracking().ToList(),
                Employees = db.Employees.AsNoTracking().ToList(),
                SystemUsers = db.SystemUsers.AsNoTracking().ToList(),
                InsurancePolicies = db.InsurancePolicies.AsNoTracking().ToList(),
                EmployeePolicies = db.EmployeePolicies.AsNoTracking().ToList(),
                Claims = db.Claims.AsNoTracking().ToList(),
                Benefits = db.Benefits.AsNoTracking().ToList(),
                Premiums = db.Premiums.AsNoTracking().ToList(),
                Documents = db.Documents.AsNoTracking().ToList(),
                DocumentTransactions = db.DocumentTransactions.AsNoTracking().ToList(),
                Senders = db.Senders.AsNoTracking().ToList(),
                Receivers = db.Receivers.AsNoTracking().ToList(),
                BeneficiaryStaging = db.BeneficiaryStaging.AsNoTracking().ToList(),
                ResidentDemographics = db.ResidentDemographics.AsNoTracking().ToList(),
                Contributions = db.Contributions.AsNoTracking().ToList(),
                Notifications = db.Notifications.AsNoTracking().ToList(),
                DocumentTypes = db.DocumentTypes.AsNoTracking().ToList(),
                ClaimDocuments = db.ClaimDocuments.AsNoTracking().ToList(),
                AuditLogs = db.AuditLogs.AsNoTracking().ToList(),

                // Non-timestamped (Full only)
                Beneficiaries = db.Beneficiaries.AsNoTracking().ToList(),
                CompanyProfiles = db.CompanyProfiles.AsNoTracking().ToList(),
                Trainings = db.Trainings.AsNoTracking().ToList()
            };

            var dir = overridePath ?? FullDir;
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, $"full_{now:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(filePath, JsonSerializer.Serialize(data, JsonOptions));

            var metadata = LoadMetadata();
            metadata.LastFullBackup = now;
            metadata.LastAnyBackup = now;
            metadata.LastFullBackupPath = filePath;
            SaveMetadata(metadata);

            return filePath;
        }

        // ── Differential Backup ────────────────────────────────────────
        // Everything changed since the LAST FULL BACKUP
        public static string ExecuteDifferentialBackup(string? overridePath = null)
        {
            var metadata = LoadMetadata();
            if (metadata.LastFullBackup is null)
                throw new InvalidOperationException(
                    "No full backup found. Create a full backup first.");

            EnsureDirectories();
            var refDate = metadata.LastFullBackup.Value;
            var now = DateTime.Now;
            using var db = eSureHiDbContextFactory.Create();

            var data = new eSureHiBackupData
            {
                BackupType = "Differential",
                CreatedAt = now,
                ReferenceDate = refDate,

                Departments = db.Departments.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Employees = db.Employees.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                SystemUsers = db.SystemUsers.AsNoTracking()
                    .Where(x => x.CreatedAt > refDate ||
                                (x.LastLogin.HasValue && x.LastLogin.Value > refDate)).ToList(),
                InsurancePolicies = db.InsurancePolicies.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                EmployeePolicies = db.EmployeePolicies.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Claims = db.Claims.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Benefits = db.Benefits.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Premiums = db.Premiums.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Documents = db.Documents.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                DocumentTransactions = db.DocumentTransactions.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Senders = db.Senders.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Receivers = db.Receivers.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                BeneficiaryStaging = db.BeneficiaryStaging.AsNoTracking()
                    .Where(x => x.ImportedAt > refDate).ToList(),
                ResidentDemographics = db.ResidentDemographics.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Contributions = db.Contributions.AsNoTracking()
                    .Where(x => x.CreatedAt > refDate).ToList(),
                Notifications = db.Notifications.AsNoTracking()
                    .Where(x => x.CreatedAt > refDate).ToList(),
                DocumentTypes = db.DocumentTypes.AsNoTracking()
                    .Where(x => x.CreatedAt > refDate).ToList(),
                ClaimDocuments = db.ClaimDocuments.AsNoTracking()
                    .Where(x => x.UploadedAt > refDate).ToList(),
                AuditLogs = db.AuditLogs.AsNoTracking()
                    .Where(x => x.LoggedAt > refDate).ToList()
            };

            var dir = overridePath ?? DifferentialDir;
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, $"diff_{now:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(filePath, JsonSerializer.Serialize(data, JsonOptions));

            metadata.LastDifferentialBackup = now;
            metadata.LastAnyBackup = now;
            SaveMetadata(metadata);

            return filePath;
        }

        // ── Incremental Backup ─────────────────────────────────────────
        // Only what changed since the LAST BACKUP OF ANY TYPE
        public static string ExecuteIncrementalBackup(string? overridePath = null)
        {
            var metadata = LoadMetadata();
            if (metadata.LastFullBackup is null)
                throw new InvalidOperationException(
                    "No full backup found. Create a full backup first.");

            EnsureDirectories();
            var refDate = metadata.LastAnyBackup ?? metadata.LastFullBackup.Value;
            var now = DateTime.Now;
            using var db = eSureHiDbContextFactory.Create();

            var data = new eSureHiBackupData
            {
                BackupType = "Incremental",
                CreatedAt = now,
                ReferenceDate = refDate,

                Departments = db.Departments.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Employees = db.Employees.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                SystemUsers = db.SystemUsers.AsNoTracking()
                    .Where(x => x.CreatedAt > refDate ||
                                (x.LastLogin.HasValue && x.LastLogin.Value > refDate)).ToList(),
                InsurancePolicies = db.InsurancePolicies.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                EmployeePolicies = db.EmployeePolicies.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Claims = db.Claims.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Benefits = db.Benefits.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Premiums = db.Premiums.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Documents = db.Documents.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                DocumentTransactions = db.DocumentTransactions.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Senders = db.Senders.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Receivers = db.Receivers.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                BeneficiaryStaging = db.BeneficiaryStaging.AsNoTracking()
                    .Where(x => x.ImportedAt > refDate).ToList(),
                ResidentDemographics = db.ResidentDemographics.AsNoTracking()
                    .Where(x => x.UpdatedAt > refDate).ToList(),
                Contributions = db.Contributions.AsNoTracking()
                    .Where(x => x.CreatedAt > refDate).ToList(),
                Notifications = db.Notifications.AsNoTracking()
                    .Where(x => x.CreatedAt > refDate).ToList(),
                DocumentTypes = db.DocumentTypes.AsNoTracking()
                    .Where(x => x.CreatedAt > refDate).ToList(),
                ClaimDocuments = db.ClaimDocuments.AsNoTracking()
                    .Where(x => x.UploadedAt > refDate).ToList(),
                AuditLogs = db.AuditLogs.AsNoTracking()
                    .Where(x => x.LoggedAt > refDate).ToList()
            };

            var dir = overridePath ?? IncrementalDir;
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, $"inc_{now:yyyyMMdd_HHmmss}.json");
            File.WriteAllText(filePath, JsonSerializer.Serialize(data, JsonOptions));

            metadata.LastIncrementalBackup = now;
            metadata.LastAnyBackup = now;
            SaveMetadata(metadata);

            return filePath;
        }

        // ── Restore ────────────────────────────────────────────────────
        public static void RestoreFromBackup(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<eSureHiBackupData>(json, JsonOptions)
                ?? throw new InvalidOperationException(
                    "Backup file is invalid or corrupted.");

            using var db = eSureHiDbContextFactory.Create();

            if (data.BackupType == "Full")
                RestoreFull(db, data);
            else
                RestorePartial(db, data);
        }

        private static void RestoreFull(Data.eSureHiDbContext db, eSureHiBackupData data)
        {
            db.Database.ExecuteSqlRaw("SET FOREIGN_KEY_CHECKS = 0;");
            try
            {
                // Clear in reverse dependency order
                db.Database.ExecuteSqlRaw("DELETE FROM audit_logs;");
                db.Database.ExecuteSqlRaw("DELETE FROM notifications;");
                db.Database.ExecuteSqlRaw("DELETE FROM document_transactions;");
                db.Database.ExecuteSqlRaw("DELETE FROM claim_documents;");
                db.Database.ExecuteSqlRaw("DELETE FROM claims;");
                db.Database.ExecuteSqlRaw("DELETE FROM benefits;");
                db.Database.ExecuteSqlRaw("DELETE FROM premiums;");
                db.Database.ExecuteSqlRaw("DELETE FROM trainings;");
                db.Database.ExecuteSqlRaw("DELETE FROM contributions;");
                db.Database.ExecuteSqlRaw("DELETE FROM employee_policies;");
                db.Database.ExecuteSqlRaw("DELETE FROM beneficiaries;");
                db.Database.ExecuteSqlRaw("DELETE FROM resident_demographics;");
                db.Database.ExecuteSqlRaw("DELETE FROM beneficiary_staging;");
                db.Database.ExecuteSqlRaw("DELETE FROM documents;");
                db.Database.ExecuteSqlRaw("DELETE FROM document_types;");
                db.Database.ExecuteSqlRaw("DELETE FROM senders;");
                db.Database.ExecuteSqlRaw("DELETE FROM receivers;");
                db.Database.ExecuteSqlRaw("DELETE FROM company_profile;");
                db.Database.ExecuteSqlRaw("DELETE FROM system_users;");
                db.Database.ExecuteSqlRaw("DELETE FROM employees;");
                db.Database.ExecuteSqlRaw("DELETE FROM insurance_policies;");
                db.Database.ExecuteSqlRaw("DELETE FROM departments;");
            }
            finally
            {
                db.Database.ExecuteSqlRaw("SET FOREIGN_KEY_CHECKS = 1;");
            }

            // Insert in dependency order — parents before children
            if (data.Departments.Any())
            { db.Departments.AddRange(data.Departments); db.SaveChanges(); }

            if (data.Employees.Any())
            { db.Employees.AddRange(data.Employees); db.SaveChanges(); }

            if (data.SystemUsers.Any())
            { db.SystemUsers.AddRange(data.SystemUsers); db.SaveChanges(); }

            if (data.InsurancePolicies.Any())
            { db.InsurancePolicies.AddRange(data.InsurancePolicies); db.SaveChanges(); }

            if (data.CompanyProfiles.Any())
            { db.CompanyProfiles.AddRange(data.CompanyProfiles); db.SaveChanges(); }

            if (data.EmployeePolicies.Any())
            { db.EmployeePolicies.AddRange(data.EmployeePolicies); db.SaveChanges(); }

            if (data.DocumentTypes.Any())
            { db.DocumentTypes.AddRange(data.DocumentTypes); db.SaveChanges(); }

            if (data.Beneficiaries.Any())
            { db.Beneficiaries.AddRange(data.Beneficiaries); db.SaveChanges(); }

            if (data.BeneficiaryStaging.Any())
            { db.BeneficiaryStaging.AddRange(data.BeneficiaryStaging); db.SaveChanges(); }

            if (data.ResidentDemographics.Any())
            { db.ResidentDemographics.AddRange(data.ResidentDemographics); db.SaveChanges(); }

            if (data.Contributions.Any())
            { db.Contributions.AddRange(data.Contributions); db.SaveChanges(); }

            if (data.Trainings.Any())
            { db.Trainings.AddRange(data.Trainings); db.SaveChanges(); }

            if (data.Claims.Any())
            { db.Claims.AddRange(data.Claims); db.SaveChanges(); }

            if (data.ClaimDocuments.Any())
            { db.ClaimDocuments.AddRange(data.ClaimDocuments); db.SaveChanges(); }

            if (data.Benefits.Any())
            { db.Benefits.AddRange(data.Benefits); db.SaveChanges(); }

            if (data.Premiums.Any())
            { db.Premiums.AddRange(data.Premiums); db.SaveChanges(); }

            if (data.Documents.Any())
            { db.Documents.AddRange(data.Documents); db.SaveChanges(); }

            if (data.Senders.Any())
            { db.Senders.AddRange(data.Senders); db.SaveChanges(); }

            if (data.Receivers.Any())
            { db.Receivers.AddRange(data.Receivers); db.SaveChanges(); }

            if (data.DocumentTransactions.Any())
            { db.DocumentTransactions.AddRange(data.DocumentTransactions); db.SaveChanges(); }

            if (data.Notifications.Any())
            { db.Notifications.AddRange(data.Notifications); db.SaveChanges(); }

            if (data.AuditLogs.Any())
            { db.AuditLogs.AddRange(data.AuditLogs); db.SaveChanges(); }
        }

        private static void RestorePartial(Data.eSureHiDbContext db, eSureHiBackupData data)
        {
            UpsertRange(db, data.Departments, db.Departments, x => x.DeptId);
            UpsertRange(db, data.Employees, db.Employees, x => x.EmpId);
            UpsertRange(db, data.SystemUsers, db.SystemUsers, x => x.UserId);
            UpsertRange(db, data.InsurancePolicies, db.InsurancePolicies, x => x.PolicyId);
            UpsertRange(db, data.EmployeePolicies, db.EmployeePolicies, x => x.EpId);
            UpsertRange(db, data.Claims, db.Claims, x => x.ClaimId);
            UpsertRange(db, data.Benefits, db.Benefits, x => x.BenefitId);
            UpsertRange(db, data.Premiums, db.Premiums, x => x.PremiumId);
            UpsertRange(db, data.Documents, db.Documents, x => x.DocumentId);
            UpsertRange(db, data.Senders, db.Senders, x => x.SenderId);
            UpsertRange(db, data.Receivers, db.Receivers, x => x.ReceiverId);
            UpsertRange(db, data.DocumentTransactions, db.DocumentTransactions, x => x.TransactionId);
            UpsertRange(db, data.BeneficiaryStaging, db.BeneficiaryStaging, x => x.StagingId);
            UpsertRange(db, data.ResidentDemographics, db.ResidentDemographics, x => x.ResidentDemographicId);
            UpsertRange(db, data.Contributions, db.Contributions, x => x.Id);
            UpsertRange(db, data.Notifications, db.Notifications, x => x.NotifId);
            UpsertRange(db, data.DocumentTypes, db.DocumentTypes, x => x.DocTypeId);
            UpsertRange(db, data.ClaimDocuments, db.ClaimDocuments, x => x.DocId);
            UpsertRange(db, data.AuditLogs, db.AuditLogs, x => x.LogId);
            UpsertRange(db, data.Beneficiaries, db.Beneficiaries, x => x.BenId);
            UpsertRange(db, data.CompanyProfiles, db.CompanyProfiles, x => x.Id);
            UpsertRange(db, data.Trainings, db.Trainings, x => x.Id);
            db.SaveChanges();
        }

        private static void UpsertRange<T, TKey>(
            Data.eSureHiDbContext db,
            System.Collections.Generic.List<T> entities,
            Microsoft.EntityFrameworkCore.DbSet<T> dbSet,
            Func<T, TKey> getKey) where T : class
        {
            foreach (var entity in entities)
            {
                var existing = dbSet.Find(getKey(entity));
                if (existing is not null)
                    db.Entry(existing).CurrentValues.SetValues(entity);
                else
                    dbSet.Add(entity);
            }
        }
    }
}
