using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("beneficiaries")]
    public class Beneficiary
    {
        [Key]
        [Column("ben_id")]
        public int BenId { get; set; }

        [Column("emp_id")]
        public int EmpId { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [Column("relationship")]
        public string? Relationship { get; set; }

        [Column("date_of_birth")]
        public DateOnly? DateOfBirth { get; set; }

        [Column("gender")]
        public string? Gender { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("is_primary")]
        public bool IsPrimary { get; set; } = false;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("beneficiary_id")]
        public string? BeneficiaryId { get; set; }

        [Column("civil_registry_id")]
        public string? CivilRegistryId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        // New requested fields
        [Column("recipients_insurance")]
        public string? RecipientsInsurance { get; set; }

        [Column("cedula_no")]
        public string? CedulaNo { get; set; }

        [Column("received")]
        public bool Received { get; set; } = false;

        [Column("contribution")]
        public decimal Contribution { get; set; } = 0;

        [Column("source_of_funds")]
        public string? SourceOfFunds { get; set; }

        [Column("workflow_status")]
        public string WorkflowStatus { get; set; } = "Pending";

        [Column("status_remarks")]
        public string? StatusRemarks { get; set; }

        [Column("is_admin_confirmed")]
        public bool IsAdminConfirmed { get; set; } = true;

        // Navigation
        [ForeignKey("EmpId")]
        public Employee? Employee { get; set; }

        [NotMapped]
        public string FullName
        {
            get => $"{FirstName} {LastName}".Trim();
            set { }
        }
    }
}
