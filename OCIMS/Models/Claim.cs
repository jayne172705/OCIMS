namespace OCIMS.Models
{
    public class Claim
    {
        public int ClaimId { get; set; }
        public string ClaimNo { get; set; }
        public string ClientName { get; set; }
        public string ClaimType { get; set; }
        public string ClaimDate { get; set; }
        public decimal Amount { get; set; }
        public string ClaimStatus { get; set; } = "Pending";
        public string Description { get; set; }
        public int EmpId { get; set; }

        public string AmountDisplay
        {
            get { return "₱" + Amount.ToString("N2"); }
        }

        public string StatusBg
        {
            get
            {
                if (ClaimStatus == "Approved") return "#E6F9F0";
                if (ClaimStatus == "Pending") return "#FEF5E7";
                if (ClaimStatus == "Rejected") return "#FDEAEA";
                if (ClaimStatus == "Released") return "#E8F2FD";
                return "#F0F4F8";
            }
        }

        public string StatusFg
        {
            get
            {
                if (ClaimStatus == "Approved") return "#1A8A4A";
                if (ClaimStatus == "Pending") return "#D68910";
                if (ClaimStatus == "Rejected") return "#C0392B";
                if (ClaimStatus == "Released") return "#2E86DE";
                return "#7A8FA6";
            }
        }
    }
}
