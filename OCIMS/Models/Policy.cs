namespace OCIMS.Models
{
    public class Policy
    {
        public int PolicyId { get; set; }
        public string PolicyNo { get; set; }
        public string PolicyName { get; set; }
        public string PolicyType { get; set; }
        public string Provider { get; set; }
        public decimal CoverageAmount { get; set; }
        public string ExpiryDate { get; set; }
        public string PolicyStatus { get; set; } = "Active";
        public string Description { get; set; }

        public string CoverageDisplay
        {
            get
            {
                if (CoverageAmount >= 1000000)
                    return "₱" + (CoverageAmount / 1000000).ToString("0.#") + "M";
                if (CoverageAmount >= 1000)
                    return "₱" + (CoverageAmount / 1000).ToString("0.#") + "K";
                return "₱" + CoverageAmount.ToString("N0");
            }
        }

        public string StatusBg
        {
            get
            {
                if (PolicyStatus == "Active") return "#E6F9F0";
                if (PolicyStatus == "Expired") return "#FDEAEA";
                if (PolicyStatus == "Lapsed") return "#FEF5E7";
                if (PolicyStatus == "Cancelled") return "#F0F4F8";
                return "#E8F2FD";
            }
        }

        public string StatusFg
        {
            get
            {
                if (PolicyStatus == "Active") return "#1A8A4A";
                if (PolicyStatus == "Expired") return "#C0392B";
                if (PolicyStatus == "Lapsed") return "#D68910";
                if (PolicyStatus == "Cancelled") return "#7A8FA6";
                return "#2E86DE";
            }
        }
    }
}
