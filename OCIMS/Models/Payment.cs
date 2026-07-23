using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("payments")]
    public class Payment
    {
        [Key]
        [Column("payment_id")]
        public int PaymentId { get; set; }

        [Column("beneficiary_id")]
        public int BeneficiaryId { get; set; }

        [Column("family_id")]
        public string? FamilyId { get; set; }

        [Column("member_name")]
        public string MemberName { get; set; } = string.Empty;

        [Column("dependent_name")]
        public string? DependentName { get; set; }

        [Column("relationship")]
        public string? Relationship { get; set; }

        [Column("billing_month")]
        public DateOnly BillingMonth { get; set; }

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

        [Column("payment_type")]
        public string PaymentType { get; set; } = "Advance"; // Advance, Regular

        [Column("source_of_funds")]
        public string? SourceOfFunds { get; set; }

        [Column("paid_at")]
        public DateOnly? PaidAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("created_by")]
        public string? CreatedBy { get; set; }

        [Column("remarks")]
        public string? Remarks { get; set; }

        [ForeignKey("BeneficiaryId")]
        public Beneficiary? Beneficiary { get; set; }
    }
}
