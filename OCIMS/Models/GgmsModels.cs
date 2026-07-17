using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("budget_allocations")]
    public class BudgetAllocation
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("master_budget_id")]
        public long? MasterBudgetId { get; set; }

        [Column("office_id")]
        public long OfficeId { get; set; }

        [Column("office_type")]
        public string OfficeType { get; set; } = "service";

        [Column("program")]
        public string? Program { get; set; }

        [Column("allocated_by")]
        public long AllocatedBy { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        [Column("remaining_amount")]
        public decimal? RemainingAmount { get; set; }

        [Column("used_amount")]
        public decimal UsedAmount { get; set; } = 0.00m;

        [Column("status")]
        public string? Status { get; set; } = "active";

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("SyncId")]
        public string? SyncId { get; set; }
    }

    [Table("yearlybudgets")]
    public class YearlyBudget
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("Year")]
        public int Year { get; set; }

        [Column("TotalAmount")]
        public decimal TotalAmount { get; set; }

        [Column("Description")]
        public string? Description { get; set; }
    }

    [Table("consolidated_transactions")]
    public class GgmsTransaction
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("beneficiary_id")]
        public string? BeneficiaryId { get; set; }

        [Column("civil_registry_id")]
        public string? CivilRegistryId { get; set; }

        [Column("project_code")]
        public string ProjectCode { get; set; } = string.Empty;

        [Column("project_name")]
        public string ProjectName { get; set; } = string.Empty;

        [Column("office_id")]
        public string OfficeId { get; set; } = string.Empty;

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Column("middle_name")]
        public string? MiddleName { get; set; }

        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [Column("office_name")]
        public string OfficeName { get; set; } = string.Empty;

        [Column("transaction_type")]
        public string TransactionType { get; set; } = string.Empty;

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("transaction_date")]
        public DateOnly TransactionDate { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Released";

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }
}
