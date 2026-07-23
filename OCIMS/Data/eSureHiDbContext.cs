using Microsoft.EntityFrameworkCore;
using eSureHi.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace eSureHi.Data
{
    public class eSureHiDbContext : DbContext
    {
        public static readonly Type[] SyncEntityTypes =
        {
            typeof(Department),
            typeof(Employee),
            typeof(SystemUser),
            typeof(UserPermission),
            typeof(InsurancePolicy),
            typeof(EmployeePolicy),
            typeof(Beneficiary),
            typeof(Claim),
            typeof(ClaimDocument),
            typeof(Benefit),
            typeof(Premium),
            typeof(DocumentType),
            typeof(Document),
            typeof(Sender),
            typeof(Receiver),
            typeof(DocumentTransaction),
            typeof(Notification),
            typeof(AuditLog),
            typeof(BeneficiaryStaging),
            typeof(ResidentDemographic),
            typeof(CompanyProfile),
            typeof(Contribution),
            typeof(Training),
            typeof(Cedula),
            typeof(SourceFund),
            typeof(DistributionBatch),
            typeof(DistributionRecord)
        };

        // ── Core Tables ────────────────────────────────────────────────
        public DbSet<Department> Departments { get; set; }
        public DbSet<Employee> Employees { get; set; }
        public DbSet<SystemUser> SystemUsers { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<InsurancePolicy> InsurancePolicies { get; set; }
        public DbSet<EmployeePolicy> EmployeePolicies { get; set; }
        public DbSet<Beneficiary> Beneficiaries { get; set; }
        public DbSet<Claim> Claims { get; set; }
        public DbSet<ClaimDocument> ClaimDocuments { get; set; }
        public DbSet<Benefit> Benefits { get; set; }
        public DbSet<Premium> Premiums { get; set; }
        public DbSet<DocumentType> DocumentTypes { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Sender> Senders { get; set; }
        public DbSet<Receiver> Receivers { get; set; }
        public DbSet<DocumentTransaction> DocumentTransactions { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<BeneficiaryStaging> BeneficiaryStaging { get; set; }
        public DbSet<ResidentDemographic> ResidentDemographics { get; set; }
        public DbSet<CompanyProfile> CompanyProfiles { get; set; }
        public DbSet<Contribution> Contributions { get; set; }
        public DbSet<Training> Trainings { get; set; }
        public DbSet<Cedula> Cedulas { get; set; }
        public DbSet<SourceFund> SourceFunds { get; set; }
        public DbSet<DistributionBatch> DistributionBatches { get; set; }
        public DbSet<DistributionRecord> DistributionRecords { get; set; }
        public DbSet<CrsBeneficiaryCache> CrsBeneficiaryCache { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<GgmsAllocationCache> GgmsAllocationCache { get; set; }

        // ── Read-Only Views ────────────────────────────────────────────
        public DbSet<VwClaimsSummary> VwClaimsSummary { get; set; }
        public DbSet<VwEmployeeCoverage> VwEmployeeCoverage { get; set; }
        public DbSet<VwPremiumStatus> VwPremiumStatus { get; set; }

        public eSureHiDbContext(DbContextOptions<eSureHiDbContext> options)
            : base(options) { }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            PrepareSyncIds();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override int SaveChanges()
        {
            PrepareSyncIds();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            PrepareSyncIds();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            PrepareSyncIds();
            return base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            ConfigureSyncIds(modelBuilder);

            // ── benefits — Remaining is STORED GENERATED, never insert/update ──

            // ── premiums — TotalAmount is STORED GENERATED, never insert/update ──
            // modelBuilder.Entity<Premium>()
            //     .Property(p => p.TotalAmount)
            //     .ValueGeneratedOnAddOrUpdate();

            // ── Views — use actual PKs from the view definitions ───────
            modelBuilder.Entity<VwClaimsSummary>()
                .ToView("vw_claims_summary");

            modelBuilder.Entity<VwEmployeeCoverage>()
                .ToView("vw_employee_coverage");

            modelBuilder.Entity<VwPremiumStatus>()
                .ToView("vw_premium_status");

            // ── AuditLog — old_values / new_values stored as JSON strings ──
            modelBuilder.Entity<AuditLog>()
                .Property(a => a.OldValues)
                .HasColumnType("json");

            modelBuilder.Entity<AuditLog>()
                .Property(a => a.NewValues)
                .HasColumnType("json");

            modelBuilder.Entity<UserPermission>()
                .HasOne(p => p.User)
                .WithOne()
                .HasForeignKey<UserPermission>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserPermission>()
                .HasIndex(p => p.UserId)
                .IsUnique();

            modelBuilder.Entity<SourceFund>()
                .HasIndex(f => f.FundName)
                .IsUnique();

            modelBuilder.Entity<CrsBeneficiaryCache>()
                .HasIndex(c => c.BeneficiaryId);

            modelBuilder.Entity<ResidentDemographic>()
                .HasIndex(d => d.ResidentsId);

            modelBuilder.Entity<ResidentDemographic>()
                .HasIndex(d => d.BeneficiaryId);

            modelBuilder.Entity<ResidentDemographic>()
                .HasIndex(d => d.CivilRegistryId);

            modelBuilder.Entity<GgmsAllocationCache>()
                .HasIndex(c => new { c.OfficeCode, c.Year })
                .IsUnique();
        }

        private static void ConfigureSyncIds(ModelBuilder modelBuilder)
        {
            foreach (var entityType in SyncEntityTypes)
            {
                modelBuilder.Entity(entityType)
                    .Property<string>("SyncId")
                    .HasColumnName("SyncId")
                    .HasColumnType("varchar(36)")
                    .HasMaxLength(36)
                    .IsRequired();

                modelBuilder.Entity(entityType)
                    .HasIndex("SyncId")
                    .IsUnique();
            }
        }

        private void PrepareSyncIds()
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State is not (EntityState.Added or EntityState.Modified))
                    continue;

                var syncProperty = entry.Metadata.FindProperty("SyncId");
                if (syncProperty is null)
                    continue;

                var current = entry.Property("SyncId").CurrentValue as string;
                if (string.IsNullOrWhiteSpace(current))
                    entry.Property("SyncId").CurrentValue = Guid.NewGuid().ToString();
            }
        }
    }
}
