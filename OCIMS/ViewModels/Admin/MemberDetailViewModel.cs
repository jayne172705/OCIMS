using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using eSureHi.Views.Admin.UserControls;

namespace eSureHi.ViewModels.Admin
{
    /// <summary>
    /// Read-only, full-page profile for a single member. Seeded from the record the
    /// caller already holds so the page paints immediately, then refreshed from the
    /// database to pick up the employee/department graph the roster query omits.
    /// </summary>
    public class MemberDetailViewModel : ObservableObject
    {
        private const string NotSet = "Not set";

        private Beneficiary _member;

        public MemberDetailViewModel(Beneficiary member)
        {
            _member = member ?? new Beneficiary();

            BackCommand = new RelayCommand(() =>
                NavigationService.Instance.NavigateTo(new ManageMembersView()));
            UpdateCommand = new RelayCommand(OpenForEdit);

            _ = LoadAsync();
        }

        public RelayCommand BackCommand { get; }
        public RelayCommand UpdateCommand { get; }

        public Beneficiary Member
        {
            get => _member;
            private set
            {
                if (SetProperty(ref _member, value))
                    OnPropertyChanged(string.Empty); // every display property derives from Member
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // ── Header ─────────────────────────────────────────────────────
        public string FullName => ManageMembersViewModel.FirstNonEmpty(Member.FullName, "Unnamed Member");
        public string DisplayName => ManageMembersViewModel.BuildDisplayName(Member);
        public string FamilyId => ManageMembersViewModel.FirstNonEmpty(
            Member.BeneficiaryId,
            Member.CivilRegistryId,
            Member.Employee?.EmployeeNo,
            $"BEN-{Member.BenId:000000}");
        public string FamilyRole => ManageMembersViewModel.BuildFamilyRole(Member);
        public string Status => ManageMembersViewModel.BuildStatus(Member);

        // ── Personal details ───────────────────────────────────────────
        public string FirstName => Or(Member.FirstName);
        public string LastName => Or(Member.LastName);
        public string Gender => Or(Member.Gender);
        public string DateOfBirth => Member.DateOfBirth?.ToString("MMMM dd, yyyy") ?? NotSet;
        public string Age => CalculateAge(Member.DateOfBirth) is int age ? $"{age} years old" : NotSet;
        public string Email => Or(Member.Email);
        public string CivilRegistryId => Or(Member.CivilRegistryId, "None");
        public string Relationship => Or(Member.Relationship, "Not specified");
        public string MemberSince => Member.CreatedAt == default
            ? NotSet
            : Member.CreatedAt.ToString("MMMM dd, yyyy");

        // ── Barangay / group ───────────────────────────────────────────
        public string Barangay => Or(Member.Employee?.Barangay);
        public string Address => BuildAddress();
        public string GroupEmploymentType => ManageMembersViewModel.FirstNonEmpty(
            Member.SourceOfFunds,
            Member.Employee?.EmploymentType,
            "Unassigned");
        public string SourceOfFunds => Or(Member.SourceOfFunds, "Unassigned");
        public string CedulaNo => Or(Member.CedulaNo, "None");

        // ── Workflow status ────────────────────────────────────────────
        public string ActiveState => Member.IsActive ? "Active record" : "Deactivated record";
        public string StatusRemarks => Or(Member.StatusRemarks, "No remarks recorded");
        public string ReceivedState => Member.Received ? "Received" : "Not yet received";
        public string Contribution => Member.Contribution.ToString("₱#,##0.00");
        public string RecipientsInsurance => Or(Member.RecipientsInsurance, "None");

        // ── Linked employee ────────────────────────────────────────────
        public bool HasEmployee => Member.Employee is not null;
        public string EmployeeNo => Or(Member.Employee?.EmployeeNo);
        public string EmployeeName => Or(Member.Employee?.FullName);
        public string EmployeePosition => Or(Member.Employee?.PositionTitle);
        public string EmployeeDepartment => Or(Member.Employee?.Department?.DeptName);
        public string EmployeeType => Or(Member.Employee?.EmploymentType);
        public string EmployeeStatus => Or(Member.Employee?.EmploymentStatus);
        public string EmployeeMobile => Or(Member.Employee?.PhoneMobile);
        public string EmployeeEmail => Or(Member.Employee?.Email);
        public string EmployeeDateHired => Member.Employee?.DateHired?.ToString("MMMM dd, yyyy") ?? NotSet;

        private async Task LoadAsync()
        {
            if (Member.BenId <= 0)
                return;

            IsLoading = true;
            StatusMessage = string.Empty;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var fresh = await db.Beneficiaries
                    .Include(b => b.Employee)
                        .ThenInclude(e => e!.Department)
                    .FirstOrDefaultAsync(b => b.BenId == Member.BenId);

                if (fresh is not null)
                    Member = fresh;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load member details failed: {ex.GetBaseException().Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OpenForEdit()
        {
            var viewModel = new BeneficiaryStagingViewModel
            {
                SelectedSource = "Insurance Beneficiaries",
                SelectedSystemBeneficiary = Member
            };

            NavigationService.Instance.NavigateTo(new BeneficiariesView(viewModel, openInitialSearch: false));
        }

        private string BuildAddress()
        {
            var employee = Member.Employee;
            if (employee is null)
                return NotSet;

            var address = ManageMembersViewModel.FirstNonEmpty(
                string.Join(", ", new[]
                {
                    employee.AddressLine1,
                    employee.AddressLine2,
                    employee.Barangay,
                    employee.City,
                    employee.Province
                }.Where(part => !string.IsNullOrWhiteSpace(part))));

            return string.IsNullOrWhiteSpace(address) ? NotSet : address;
        }

        private static int? CalculateAge(DateOnly? dateOfBirth)
        {
            if (dateOfBirth is null)
                return null;

            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - dateOfBirth.Value.Year;
            if (dateOfBirth.Value > today.AddYears(-age))
                age--;

            return age < 0 ? null : age;
        }

        private static string Or(string? value, string fallback = NotSet) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
