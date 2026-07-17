using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("premiums")]
    public class Premium
    {
        [Key]
        [Column("premium_id")]
        public int PremiumId { get; set; }

        [Column("ep_id")]
        public int EpId { get; set; }

        [Column("billing_month")]
        public DateOnly BillingMonth { get; set; }

        [Column("employee_amount")]
        public decimal EmployeeAmount { get; set; } = 0;

        [Column("employer_amount")]
        public decimal EmployerAmount { get; set; } = 0;

        // STORED GENERATED — never write to this column
        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("due_date")]
        public DateOnly DueDate { get; set; }

        [Column("paid_date")]
        public DateOnly? PaidDate { get; set; }

        [Column("payment_mode")]
        public string PaymentMode { get; set; } = "Payroll Deduction";

        [Column("payment_status")]
        public string PaymentStatus { get; set; } = "Unpaid";

        [Column("amount_paid")]
        public decimal AmountPaid { get; set; } = 0;

        [Column("balance")]
        public decimal Balance { get; set; } = 0;

        [Column("reference_no")]
        public string? ReferenceNo { get; set; }

        [Column("late_fee")]
        public decimal LateFee { get; set; } = 0;

        [Column("remarks")]
        public string? Remarks { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // Navigation
        [ForeignKey("EpId")]
        public EmployeePolicy? EmployeePolicy { get; set; }
    }
}
