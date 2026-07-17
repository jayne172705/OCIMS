using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("employee_policies")]
    public class EmployeePolicy
    {
        [Key]
        [Column("ep_id")]
        public int EpId { get; set; }

        [Column("emp_id")]
        public int EmpId { get; set; }

        [Column("policy_id")]
        public int PolicyId { get; set; }

        [Column("coverage_limit")]
        public decimal CoverageLimit { get; set; } = 0;

        [Column("employee_share")]
        public decimal EmployeeShare { get; set; } = 0;

        [Column("employer_share")]
        public decimal EmployerShare { get; set; } = 0;

        [Column("start_date")]
        public DateOnly? StartDate { get; set; }

        [Column("end_date")]
        public DateOnly? EndDate { get; set; }

        [Column("assignment_status")]
        public string AssignmentStatus { get; set; } = "Pending";

        [Column("status_remarks")]
        public string? StatusRemarks { get; set; }

        [Column("remarks")]
        public string? Remarks { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // Navigation
        [ForeignKey("EmpId")]
        public Employee? Employee { get; set; }

        [ForeignKey("PolicyId")]
        public InsurancePolicy? Policy { get; set; }
    }
}
