using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("insurance_policies")]
    public class InsurancePolicy
    {
        [Key]
        [Column("policy_id")]
        public int PolicyId { get; set; }

        [Column("policy_code")]
        public string PolicyCode { get; set; } = string.Empty;

        [Column("policy_name")]
        public string PolicyName { get; set; } = string.Empty;

        [Column("policy_type")]
        public string PolicyType { get; set; } = string.Empty;

        [Column("provider_name")]
        public string? ProviderName { get; set; }

        [Column("provider_contact")]
        public string? ProviderContact { get; set; }

        [Column("effective_date")]
        public DateOnly? EffectiveDate { get; set; }

        [Column("expiry_date")]
        public DateOnly? ExpiryDate { get; set; }

        [Column("renewal_date")]
        public DateOnly? RenewalDate { get; set; }

        [Column("coverage_amount")]
        public decimal CoverageAmount { get; set; } = 0;

        [Column("description")]
        public string? Description { get; set; }

        [Column("terms_conditions")]
        public string? TermsConditions { get; set; }

        [Column("policy_status")]
        public string PolicyStatus { get; set; } = "Active";

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
