using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("senders")]
    public class Sender
    {
        [Key]
        [Column("sender_id")]
        public int SenderId { get; set; }

        [Column("sender_name")]
        public string SenderName { get; set; } = string.Empty;

        [Column("sender_type")]
        public string SenderType { get; set; } = "External";

        [Column("email")]
        public string? Email { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("address")]
        public string? Address { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}
