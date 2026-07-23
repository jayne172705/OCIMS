using System;

namespace eSureHi.Models
{
    public class MemberReportRow
    {
        public string BeneficiaryCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string HouseholdId { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty;
        public string Program { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TotalClaims { get; set; }
        public decimal TotalClaimed { get; set; }
        public int TotalPayments { get; set; }
        public decimal TotalPaymentAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
