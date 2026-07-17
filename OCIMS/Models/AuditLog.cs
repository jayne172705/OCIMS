using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("audit_logs")]
    public class AuditLog
    {
        [Key]
        [Column("log_id")]
        public long LogId { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("action")]
        public string Action { get; set; } = string.Empty;

        [Column("table_name")]
        public string? TableName { get; set; }

        [Column("record_id")]
        public int? RecordId { get; set; }

        [Column("old_values")]
        public string? OldValues { get; set; }

        [Column("new_values")]
        public string? NewValues { get; set; }

        [Column("ip_address")]
        public string? IpAddress { get; set; }

        [Column("logged_at")]
        public DateTime LoggedAt { get; set; }
    }
}
