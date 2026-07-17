using System;
using System.Collections.Generic;

namespace eSureHi.Models
{
    // ── Metadata ───────────────────────────────────────────────────────
    public class BackupMetadata
    {
        public DateTime? LastFullBackup { get; set; }
        public DateTime? LastDifferentialBackup { get; set; }
        public DateTime? LastIncrementalBackup { get; set; }
        public DateTime? LastAnyBackup { get; set; }
        public string? LastFullBackupPath { get; set; } = string.Empty;
        public string? DefaultBackupPath { get; set; }
    }

    // ── Backup Data ────────────────────────────────────────────────────
    // Full        → all tables, ReferenceDate = null
    // Differential → rows changed since last Full
    // Incremental  → rows changed since last ANY backup
    //
    // Tables WITHOUT an update timestamp may still participate in partial backups
    // when they have a creation, import, or log timestamp available.
    public class eSureHiBackupData
    {
        public string BackupType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ReferenceDate { get; set; }
        public string AppVersion { get; set; } = "1.0.0";

        // ── Timestamped tables (all backup types) ──────────────────────
        public List<Department> Departments { get; set; } = new();
        public List<Employee> Employees { get; set; } = new();
        public List<SystemUser> SystemUsers { get; set; } = new();
        public List<InsurancePolicy> InsurancePolicies { get; set; } = new();
        public List<EmployeePolicy> EmployeePolicies { get; set; } = new();
        public List<Claim> Claims { get; set; } = new();
        public List<Benefit> Benefits { get; set; } = new();
        public List<Premium> Premiums { get; set; } = new();
        public List<Document> Documents { get; set; } = new();
        public List<DocumentTransaction> DocumentTransactions { get; set; } = new();
        public List<Sender> Senders { get; set; } = new();
        public List<Receiver> Receivers { get; set; } = new();
        public List<BeneficiaryStaging> BeneficiaryStaging { get; set; } = new();
        public List<ResidentDemographic> ResidentDemographics { get; set; } = new();
        public List<Contribution> Contributions { get; set; } = new();
        public List<Notification> Notifications { get; set; } = new();
        public List<DocumentType> DocumentTypes { get; set; } = new();
        public List<ClaimDocument> ClaimDocuments { get; set; } = new();
        public List<AuditLog> AuditLogs { get; set; } = new();

        // ── Non-timestamped tables (Full backup only) ──────────────────
        public List<Beneficiary> Beneficiaries { get; set; } = new();
        public List<CompanyProfile> CompanyProfiles { get; set; } = new();
        public List<Training> Trainings { get; set; } = new();
    }

    // ── Backup Info ────────────────────────────────────────────────────
    public class BackupInfo
    {
        public BackupMetadata Metadata { get; set; } = new();
        public int FullBackupCount { get; set; }
        public int DifferentialBackupCount { get; set; }
        public int IncrementalBackupCount { get; set; }
    }
}
