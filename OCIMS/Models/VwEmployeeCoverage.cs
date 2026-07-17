using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("vw_employee_coverage")]
    public class VwEmployeeCoverage
    {
        [Key]
        [Column("ep_id")]
        public int EpId { get; set; }

        [Column("emp_id")]
        public int EmpId { get; set; }

        [Column("employee_no")]
        public string? EmployeeNo { get; set; }

        [Column("full_name")]
        public string? FullName { get; set; }

        [Column("dept_name")]
        public string? DeptName { get; set; }

        [Column("barangay")]
        public string? Barangay { get; set; }

        [Column("policy_name")]
        public string? PolicyName { get; set; }

        [Column("policy_type")]
        public string? PolicyType { get; set; }

        [Column("coverage_limit")]
        public decimal CoverageLimit { get; set; }

        [Column("employee_share")]
        public decimal EmployeeShare { get; set; }

        [Column("employer_share")]
        public decimal EmployerShare { get; set; }

        [Column("start_date")]
        public DateOnly? StartDate { get; set; }

        [Column("end_date")]
        public DateOnly? EndDate { get; set; }

        [Column("assignment_status")]
        public string? AssignmentStatus { get; set; }
    }
}
