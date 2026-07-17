using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("receivers")]
    public class Receiver
    {
        [Key]
        [Column("receiver_id")]
        public int ReceiverId { get; set; }

        [Column("receiver_name")]
        public string ReceiverName { get; set; } = string.Empty;

        [Column("receiver_type")]
        public string ReceiverType { get; set; } = "External";

        [Column("email")]
        public string? Email { get; set; }

        [Column("phone")]
        public string? Phone { get; set; }

        [Column("department")]
        public string? Department { get; set; }

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
