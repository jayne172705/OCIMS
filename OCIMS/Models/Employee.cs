namespace OCIMS.Models
{
    public class Employee
    {
        public string EmployeeNo { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string Suffix { get; set; }
        public string FullName => FirstName + " " + LastName;
        public string Initials
        {
            get
            {
                string f = (FirstName != null && FirstName.Length > 0) ? FirstName[0].ToString() : "";
                string l = (LastName != null && LastName.Length > 0) ? LastName[0].ToString() : "";
                return f + l;
            }
        }
        public string Gender { get; set; }
        public string CivilStatus { get; set; }
        public string Email { get; set; }
        public string PhoneMobile { get; set; }
        public string PhoneOffice { get; set; }
        public string Address { get; set; }
        public string Department { get; set; }
        public string Position { get; set; }
        public string EmploymentType { get; set; }
        public string EmploymentStatus { get; set; } = "Active";
        public System.DateTime DateHired { get; set; }
        public System.DateTime DateOfBirth { get; set; }
        public string PolicyCount { get; set; } = "0 Policies";

        // Badge background color
        public string StatusBg
        {
            get
            {
                if (EmploymentStatus == "Active") return "#E6F9F0";
                if (EmploymentStatus == "Inactive") return "#F0F4F8";
                if (EmploymentStatus == "Retired") return "#FEF5E7";
                if (EmploymentStatus == "Terminated") return "#FDEAEA";
                return "#F0F4F8";
            }
        }

        // Badge text color
        public string StatusFg
        {
            get
            {
                if (EmploymentStatus == "Active") return "#1A8A4A";
                if (EmploymentStatus == "Inactive") return "#7A8FA6";
                if (EmploymentStatus == "Retired") return "#D68910";
                if (EmploymentStatus == "Terminated") return "#C0392B";
                return "#7A8FA6";
            }
        }
    }
}