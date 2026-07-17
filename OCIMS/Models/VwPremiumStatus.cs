using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("vw_premium_status")]
    public class VwPremiumStatus
    {
        [Key]
        [Column("premium_id")]
        public int PremiumId { get; set; }

        [Column("billing_month")]
        public DateOnly BillingMonth { get; set; }

        [Column("due_date")]
        public DateOnly DueDate { get; set; }

        [Column("employee_name")]
        public string? EmployeeName { get; set; }

        [Column("policy_name")]
        public string? PolicyName { get; set; }

        [Column("total_amount")]
        public decimal TotalAmount { get; set; }

        [Column("amount_paid")]
        public decimal AmountPaid { get; set; }

        [Column("balance")]
        public decimal Balance { get; set; }

        [Column("payment_status")]
        public string? PaymentStatus { get; set; }
    }
}
