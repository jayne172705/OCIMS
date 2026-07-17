using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("document_transactions")]
    public class DocumentTransaction
    {
        [Key]
        [Column("transaction_id")]
        public int TransactionId { get; set; }

        [Column("transaction_no")]
        public string TransactionNo { get; set; } = string.Empty;

        [Column("document_id")]
        public int? DocumentId { get; set; }

        [Column("transaction_type")]
        public string TransactionType { get; set; } = string.Empty;

        [Column("sender_id")]
        public int? SenderId { get; set; }

        [Column("receiver_id")]
        public int? ReceiverId { get; set; }

        [Column("subject")]
        public string Subject { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("transaction_date")]
        public DateOnly? TransactionDate { get; set; }

        [Column("due_date")]
        public DateOnly? DueDate { get; set; }

        [Column("priority")]
        public string Priority { get; set; } = "Normal";

        [Column("status")]
        public string Status { get; set; } = "Pending";

        [Column("remarks")]
        public string? Remarks { get; set; }

        [Column("processed_by")]
        public int? ProcessedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // Navigation
        [ForeignKey("SenderId")]
        public Sender? Sender { get; set; }

        [ForeignKey("ReceiverId")]
        public Receiver? Receiver { get; set; }
    }
}
