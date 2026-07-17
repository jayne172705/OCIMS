using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("system_users")]
    public class SystemUser
    {
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("emp_id")]
        public int? EmpId { get; set; }

        [Column("ben_id")]
        public int? BenId { get; set; }

        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("role")]
        public string Role { get; set; } = "Employee";

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        // Navigation
        [ForeignKey("EmpId")]
        public Employee? Employee { get; set; }

        [ForeignKey("BenId")]
        public Beneficiary? Beneficiary { get; set; }
    }
}
