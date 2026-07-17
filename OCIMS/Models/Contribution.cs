using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("contributions")]
    public class Contribution
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("employee_id")]
        public int EmployeeId { get; set; }
        
        [ForeignKey(nameof(EmployeeId))]
        public Employee? Employee { get; set; }

        [Column("contribution_date")]
        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("remarks")]
        public string? Remarks { get; set; }
        
        [Column("or_number")]
        public string? OrNumber { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
