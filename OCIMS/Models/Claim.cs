using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("claims")]
    public class Claim
    {
        [Key]
        [Column("claim_id")]
        public int ClaimId { get; set; }

        [Column("claim_no")]
        public string ClaimNo { get; set; } = string.Empty;

        [Column("emp_id")]
        public int EmpId { get; set; }

        [Column("policy_id")]
        public int PolicyId { get; set; }

        [Column("ben_id")]
        public int? BenId { get; set; }

        [Column("claim_type")]
        public string ClaimType { get; set; } = string.Empty;

        [Column("claim_date")]
        public DateOnly? ClaimDate { get; set; }

        [Column("incident_date")]
        public DateOnly? IncidentDate { get; set; }

        [Column("incident_description")]
        public string? IncidentDescription { get; set; }

        [Column("hospital_clinic")]
        public string? HospitalClinic { get; set; }

        [Column("attending_physician")]
        public string? AttendingPhysician { get; set; }

        [Column("amount_claimed")]
        public decimal AmountClaimed { get; set; } = 0;

        [Column("admission_date")]
        public DateOnly? AdmissionDate { get; set; }

        [Column("discharge_date")]
        public DateOnly? DischargeDate { get; set; }

        [Column("excess_bill_amount")]
        public decimal ExcessBillAmount { get; set; } = 0;

        [Column("outside_diagnostics_amount")]
        public decimal OutsideDiagnosticsAmount { get; set; } = 0;

        [Column("total_covered")]
        public decimal TotalCovered { get; set; } = 0;

        [Column("admission_days")]
        public int AdmissionDays { get; set; } = 0;

        [Column("covered_allowance_days")]
        public int CoveredAllowanceDays { get; set; } = 0;

        [Column("daily_allowance_rate")]
        public decimal DailyAllowanceRate { get; set; } = 0;

        [Column("daily_allowance_amount")]
        public decimal DailyAllowanceAmount { get; set; } = 0;

        [Column("amount_approved")]
        public decimal AmountApproved { get; set; } = 0;

        [Column("amount_released")]
        public decimal AmountReleased { get; set; } = 0;

        [Column("source_of_funds")]
        public string? SourceOfFunds { get; set; }

        [Column("claim_status")]
        public string ClaimStatus { get; set; } = "Draft";

        [Column("submitted_date")]
        public DateTime? SubmittedDate { get; set; }

        [Column("reviewed_date")]
        public DateTime? ReviewedDate { get; set; }

        [Column("approved_date")]
        public DateTime? ApprovedDate { get; set; }

        [Column("released_date")]
        public DateTime? ReleasedDate { get; set; }

        [Column("reviewed_by")]
        public int? ReviewedBy { get; set; }

        [Column("approved_by")]
        public int? ApprovedBy { get; set; }

        [Column("rejection_reason")]
        public string? RejectionReason { get; set; }

        [Column("remarks")]
        public string? Remarks { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [NotMapped]
        public string DisplayBeneficiaryId
        {
            get
            {
                if (Beneficiary != null)
                {
                    if (!string.IsNullOrWhiteSpace(Beneficiary.BeneficiaryId) &&
                        !string.Equals(Beneficiary.BeneficiaryId, "None", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(Beneficiary.BeneficiaryId, "Not set", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(Beneficiary.BeneficiaryId, "Not specified", StringComparison.OrdinalIgnoreCase))
                    {
                        return Beneficiary.BeneficiaryId.Trim();
                    }
                    return $"BEN-{Beneficiary.BenId:000000}";
                }
                return string.Empty;
            }
        }

        // Navigation
        [ForeignKey("EmpId")]
        public Employee? Employee { get; set; }

        [ForeignKey("PolicyId")]
        public InsurancePolicy? Policy { get; set; }

        [ForeignKey("BenId")]
        public Beneficiary? Beneficiary { get; set; }
    }
}
