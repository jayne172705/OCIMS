using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("notifications")]
    public class Notification
    {
        [Key]
        [Column("notif_id")]
        public int NotifId { get; set; }

        [Column("recipient_id")]
        public int RecipientId { get; set; }

        [Column("notif_type")]
        public string NotifType { get; set; } = string.Empty;

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("message")]
        public string? Message { get; set; }

        [Column("is_read")]
        public bool IsRead { get; set; } = false;

        [Column("related_id")]
        public int? RelatedId { get; set; }

        [Column("related_table")]
        public string? RelatedTable { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
