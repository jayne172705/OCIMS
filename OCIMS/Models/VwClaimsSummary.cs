using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("vw_claims_summary")]
    public class VwClaimsSummary
    {
        [Key]
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

        [Column("total_claims")]
        public int TotalClaims { get; set; }

        [Column("total_claimed")]
        public decimal TotalClaimed { get; set; }

        [Column("total_approved")]
        public decimal TotalApproved { get; set; }

        [Column("total_released")]
        public decimal TotalReleased { get; set; }

        [Column("pending_claims")]
        public int PendingClaims { get; set; }

        [Column("approved_claims")]
        public int ApprovedClaims { get; set; }

        [Column("rejected_claims")]
        public int RejectedClaims { get; set; }
    }
}
