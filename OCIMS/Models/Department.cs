using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("departments")]
    public class Department
    {
        [Key]
        [Column("dept_id")]
        public int DeptId { get; set; }

        [Column("dept_name")]
        public string DeptName { get; set; } = string.Empty;

        [Column("dept_code")]
        public string DeptCode { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
