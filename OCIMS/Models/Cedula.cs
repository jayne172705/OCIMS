using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    public class Cedula
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        
        public int EmployeeId { get; set; }

        [Required]
        [StringLength(50)]
        public string CedulaNo { get; set; } = string.Empty;

        public DateTime IssueDate { get; set; } = DateTime.Today;

        [StringLength(100)]
        public string PlaceIssued { get; set; } = string.Empty;

        public decimal AmountPaid { get; set; }

        public string Remarks { get; set; } = string.Empty;

        public string WorkflowStatus { get; set; } = "Pending";

        public string? StatusRemarks { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }
    }
}
