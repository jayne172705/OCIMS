using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("officeallocations")]
    public class BudgetAllocation
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("YearlyBudgetId")]
        public int YearlyBudgetId { get; set; }

        [Column("office_code")]
        public string OfficeCode { get; set; } = string.Empty;

        [Column("AllocatedAmount")]
        public decimal AllocatedAmount { get; set; }

        [Column("SpentAmount")]
        public decimal SpentAmount { get; set; }

        [Column("SyncId")]
        public string? SyncId { get; set; }

        [Column("UpdatedAt")]
        public DateTime? LegacyUpdatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("office_id")]
        public long? OfficeId { get; set; }
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
