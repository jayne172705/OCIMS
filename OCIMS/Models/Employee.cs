using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace eSureHi.Models
{
    [Table("employees")]
    public class Employee : INotifyPropertyChanged
    {
        private bool _isSelectedForDeletion;

        [Key]
        [Column("emp_id")]
        public int EmpId { get; set; }

        [Column("employee_no")]
        public string EmployeeNo { get; set; } = string.Empty;

        [Column("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [Column("middle_name")]
        public string? MiddleName { get; set; }

        [Column("last_name")]
        public string LastName { get; set; } = string.Empty;

        [Column("suffix")]
        public string? Suffix { get; set; }

        [Column("date_of_birth")]
        public DateOnly? DateOfBirth { get; set; }

        [Column("gender")]
        public string? Gender { get; set; }

        [Column("civil_status")]
        public string CivilStatus { get; set; } = "Single";

        [Column("nationality")]
        public string Nationality { get; set; } = "Filipino";

        [Column("email")]
        public string? Email { get; set; }

        [Column("phone_mobile")]
        public string? PhoneMobile { get; set; }

        [Column("phone_office")]
        public string? PhoneOffice { get; set; }

        [Column("address_line1")]
        public string? AddressLine1 { get; set; }

        [Column("address_line2")]
        public string? AddressLine2 { get; set; }

        [Column("city")]
        public string? City { get; set; }

        [Column("barangay")]
        public string? Barangay { get; set; }

        [Column("province")]
        public string? Province { get; set; }

        [Column("zip_code")]
        public string? ZipCode { get; set; }

        [Column("dept_id")]
        public int? DeptId { get; set; }

        [Column("position_title")]
        public string? PositionTitle { get; set; }

        [Column("employment_type")]
        public string EmploymentType { get; set; } = "Regular";

        [Column("date_hired")]
        public DateOnly? DateHired { get; set; }

        [Column("date_separated")]
        public DateOnly? DateSeparated { get; set; }

        [Column("employment_status")]
        public string EmploymentStatus { get; set; } = "Active";

        [Column("photo_path")]
        public string? PhotoPath { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // Navigation
        [ForeignKey("DeptId")]
        public Department? Department { get; set; }

        public ICollection<Contribution> Contributions { get; set; } = new List<Contribution>();
        public ICollection<Training> Trainings { get; set; } = new List<Training>();
        public ICollection<Beneficiary> Beneficiaries { get; set; } = new List<Beneficiary>();
        public ICollection<EmployeePolicy> EmployeePolicies { get; set; } = new List<EmployeePolicy>();

        [NotMapped]
        public bool IsSelectedForDeletion
        {
            get => _isSelectedForDeletion;
            set
            {
                if (_isSelectedForDeletion == value) return;
                _isSelectedForDeletion = value;
                OnPropertyChanged();
            }
        }

        [NotMapped]
        public string FullName
        {
            get => $"{FirstName} {(string.IsNullOrWhiteSpace(MiddleName) ? "" : MiddleName + " ")}{LastName}{(string.IsNullOrWhiteSpace(Suffix) ? "" : " " + Suffix)}".Trim();
            set { }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
