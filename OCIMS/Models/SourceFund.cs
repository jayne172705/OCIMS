using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("source_funds")]
    public class SourceFund
    {
        [Key]
        [Column("source_fund_id")]
        public int SourceFundId { get; set; }

        [Column("fund_name")]
        public string FundName { get; set; } = string.Empty;

        [Column("fund_type")]
        public string FundType { get; set; } = "LGU";

        [Column("description")]
        public string? Description { get; set; }

        [Column("allocated_amount")]
        public decimal AllocatedAmount { get; set; }

        [Column("used_amount")]
        public decimal UsedAmount { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Active";

        [Column("ggms_office_code")]
        public string? GgmsOfficeCode { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public decimal RemainingAmount => AllocatedAmount - UsedAmount;
    }
}
