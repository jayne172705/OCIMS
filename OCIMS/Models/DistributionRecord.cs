using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("distribution_records")]
    public class DistributionRecord
    {
        [Key]
        [Column("record_id")]
        public int RecordId { get; set; }

        [Column("batch_id")]
        public int BatchId { get; set; }

        [Column("beneficiary_id")]
        public int BeneficiaryId { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Unreleased"; // Unreleased, Pending, Released

        [Column("remarks")]
        public string? Remarks { get; set; } // Claimed, Unclaimed, Not Eligible

        [Column("processed_at")]
        public DateTime? ProcessedAt { get; set; }

        // True once this record's share has been debited from a source fund — set at
        // whichever site actually charges the fund (Confirm Batch or Admin approval).
        // Persisted so the guard holds across sessions, not just within one board.
        [Column("fund_debited")]
        public bool FundDebited { get; set; }

        [ForeignKey("BatchId")]
        public DistributionBatch? Batch { get; set; }

        [ForeignKey("BeneficiaryId")]
        public Beneficiary? Beneficiary { get; set; }
    }
}
