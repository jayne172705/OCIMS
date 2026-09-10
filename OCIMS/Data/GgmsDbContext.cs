using Microsoft.EntityFrameworkCore;
using eSureHi.Models;

namespace eSureHi.Data
{
    public class GgmsDbContext : DbContext
    {
        public GgmsDbContext(DbContextOptions<GgmsDbContext> options)
            : base(options) { }

        public DbSet<BudgetAllocation> BudgetAllocations { get; set; }
        public DbSet<YearlyBudget> YearlyBudgets { get; set; }
        public DbSet<GgmsTransaction> GgmsTransactions { get; set; }
        public DbSet<GgmsProjectDetail> ProjectDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BudgetAllocation>().ToTable("officeallocations");
            modelBuilder.Entity<YearlyBudget>().ToTable("yearlybudgets");
            modelBuilder.Entity<GgmsTransaction>().ToTable("consolidated_transactions");
            modelBuilder.Entity<GgmsProjectDetail>().ToTable("project_details");
        }
    }
}
