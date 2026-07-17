using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("benefits")]
    public class Benefit
    {
        [Key]
        [Column("benefit_id")]
        public int BenefitId { get; set; }

        [Column("ep_id")]
        public int EpId { get; set; }

        [Column("benefit_type")]
        public string BenefitType { get; set; } = string.Empty;

        [Column("year_period")]
        public int? YearPeriod { get; set; }

        [Column("max_benefit")]
        public decimal MaxBenefit { get; set; } = 0;

        [Column("used_benefit")]
        public decimal UsedBenefit { get; set; } = 0;

        [Column("source_of_funds")]
        public string? SourceOfFunds { get; set; }

        // STORED GENERATED — never write to this column
        [Column("remaining")]
        public decimal Remaining { get; set; } = 0;

        [Column("last_used_date")]
        public DateOnly? LastUsedDate { get; set; }

        [Column("notes")]
        public string? Notes { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // Navigation
        [ForeignKey("EpId")]
        public EmployeePolicy? EmployeePolicy { get; set; }
    }
}
