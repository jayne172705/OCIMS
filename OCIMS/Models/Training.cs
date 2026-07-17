using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("trainings")]
    public class Training
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("employee_id")]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("date_attended")]
        public DateOnly DateAttended { get; set; }

        [Column("duration_hours")]
        public int DurationHours { get; set; }

        [Column("instructor_or_venue")]
        public string? InstructorOrVenue { get; set; }
    }
}
