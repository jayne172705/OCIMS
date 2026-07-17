namespace eSureHi.Models
{
    public static class WorkflowStatuses
    {
        public const string Pending = "Pending";
        public const string UnderReview = "Under Review";
        public const string Verified = "Verified";
        public const string Approved = "Approved";
        public const string Released = "Released";
        public const string Rejected = "Rejected";
        public const string Archived = "Archived";

        public static string[] All => new[]
        {
            Pending,
            UnderReview,
            Verified,
            Approved,
            Released,
            Rejected,
            Archived
        };
    }
}
