// Payment.cs — place in Models folder
namespace OCIMS.Models
{
    public class Payment
    {
        public int PaymentId { get; set; }
        public string PaymentNo { get; set; }
        public string ClientName { get; set; }
        public decimal Amount { get; set; }
        public string PaymentDate { get; set; }
        public string PaymentMode { get; set; }
        public string PaymentStatus { get; set; } = "Paid";
        public string Notes { get; set; }
        public int EmpId { get; set; }

        public string AmountDisplay
        {
            get { return "₱" + Amount.ToString("N2"); }
        }

        public string StatusBg
        {
            get
            {
                if (PaymentStatus == "Paid") return "#E6F9F0";
                if (PaymentStatus == "Unpaid") return "#FDEAEA";
                if (PaymentStatus == "Partial") return "#FEF5E7";
                return "#F0F4F8";
            }
        }

        public string StatusFg
        {
            get
            {
                if (PaymentStatus == "Paid") return "#1A8A4A";
                if (PaymentStatus == "Unpaid") return "#C0392B";
                if (PaymentStatus == "Partial") return "#D68910";
                return "#7A8FA6";
            }
        }
    }
}
