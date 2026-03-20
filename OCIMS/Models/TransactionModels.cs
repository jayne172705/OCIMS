namespace OCIMS.Models
{
    public class Sender
    {
        public int SenderId { get; set; }
        public string SenderName { get; set; }
        public string SenderType { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
    }

    public class Receiver
    {
        public int ReceiverId { get; set; }
        public string ReceiverName { get; set; }
        public string ReceiverType { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Department { get; set; }
        public string Address { get; set; }
    }

    public class DocumentTransaction
    {
        public int TransactionId { get; set; }
        public string TransactionNo { get; set; }
        public string TransactionType { get; set; }
        public string SenderName { get; set; }
        public string ReceiverName { get; set; }
        public string Subject { get; set; }
        public string Description { get; set; }
        public string TransactionDate { get; set; }
        public string DueDate { get; set; }
        public string Priority { get; set; }
        public string Status { get; set; }
        public string Remarks { get; set; }
        public string DocumentTitle { get; set; }

        public string TypeIcon
        {
            get
            {
                if (TransactionType == "Incoming") return "📥";
                if (TransactionType == "Outgoing") return "📤";
                if (TransactionType == "Internal Transfer") return "🔄";
                return "📄";
            }
        }

        public string PriorityBg
        {
            get
            {
                if (Priority == "Urgent") return "#FDEAEA";
                if (Priority == "High") return "#FEF5E7";
                if (Priority == "Normal") return "#E8F2FD";
                return "#F0F4F8";
            }
        }

        public string PriorityFg
        {
            get
            {
                if (Priority == "Urgent") return "#C0392B";
                if (Priority == "High") return "#D68910";
                if (Priority == "Normal") return "#2E86DE";
                return "#7A8FA6";
            }
        }

        public string StatusBg
        {
            get
            {
                if (Status == "Completed") return "#E6F9F0";
                if (Status == "Received") return "#E8F2FD";
                if (Status == "Acknowledged") return "#E8F2FD";
                if (Status == "Pending") return "#FEF5E7";
                if (Status == "In Transit") return "#FEF5E7";
                if (Status == "Cancelled") return "#FDEAEA";
                return "#F0F4F8";
            }
        }

        public string StatusFg
        {
            get
            {
                if (Status == "Completed") return "#1A8A4A";
                if (Status == "Received") return "#2E86DE";
                if (Status == "Acknowledged") return "#2E86DE";
                if (Status == "Pending") return "#D68910";
                if (Status == "In Transit") return "#D68910";
                if (Status == "Cancelled") return "#C0392B";
                return "#7A8FA6";
            }
        }
    }
}
