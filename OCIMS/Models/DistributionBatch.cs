using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("distribution_batches")]
    public class DistributionBatch
    {
        [Key]
        [Column("batch_id")]
        public int BatchId { get; set; }

        [Column("project_code")]
        public string ProjectCode { get; set; } = string.Empty;

        [Column("project_title")]
        public string ProjectTitle { get; set; } = string.Empty;

        [Column("project_description")]
        public string? ProjectDescription { get; set; }

        [Column("source_fund_id")]
        public int? SourceFundId { get; set; }

        [Column("amount_per_beneficiary")]
        public decimal AmountPerBeneficiary { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("SourceFundId")]
        public SourceFund? SourceFund { get; set; }
        
        public ICollection<DistributionRecord> Records { get; set; } = new List<DistributionRecord>();
    }
}
