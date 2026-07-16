namespace OCIMS.Models
{
    public class FundSource
    {
        public int FundId { get; set; }
        public string FundName { get; set; }
        public string FundType { get; set; }
        public decimal Amount { get; set; }
        public string Source { get; set; }
        public string DateReceived { get; set; }
        public string FundStatus { get; set; } = "Active";
        public string Notes { get; set; }

        public string AmountDisplay
        {
            get { return "₱" + Amount.ToString("N2"); }
        }

        public string Icon
        {
            get
            {
                if (FundType == "Government") return "🏛";
                if (FundType == "Donation") return "🤝";
                if (FundType == "Grant") return "📜";
                if (FundType == "Budget") return "💼";
                if (FundType == "Premium") return "💰";
                return "💵";
            }
        }

        public string StatusBg
        {
            get
            {
                if (FundStatus == "Active") return "#E6F9F0";
                if (FundStatus == "Depleted") return "#FDEAEA";
                if (FundStatus == "On Hold") return "#FEF5E7";
                return "#F0F4F8";
            }
        }

        public string StatusFg
        {
            get
            {
                if (FundStatus == "Active") return "#1A8A4A";
                if (FundStatus == "Depleted") return "#C0392B";
                if (FundStatus == "On Hold") return "#D68910";
                return "#7A8FA6";
            }
        }
    }
}
