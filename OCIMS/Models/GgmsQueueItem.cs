using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("ggms_queue_items")]
    public class GgmsQueueItem
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("project_code")]
        public string ProjectCode { get; set; } = string.Empty;

        [Column("project_name")]
        public string ProjectName { get; set; } = string.Empty;

        [Column("beneficiary_id")]
        public string? BeneficiaryId { get; set; }

        [Column("civil_registry_id")]
        public string? CivilRegistryId { get; set; }

        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Column("middle_name")]
        public string? MiddleName { get; set; }

        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("transaction_type")]
        public string TransactionType { get; set; } = string.Empty;

        [Column("amount")]
        public decimal Amount { get; set; }

        [Column("transaction_date")]
        public DateTime TransactionDate { get; set; }

        [Column("status")]
        public string Status { get; set; } = "Pending"; // "Pending", "Success", "Failed"

        [Column("error_message")]
        public string? ErrorMessage { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
