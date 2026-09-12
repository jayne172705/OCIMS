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
        private readonly BeneficiaryStaging? _stagingRecord;

        public MemberDetailViewModel(Beneficiary member, BeneficiaryStaging? stagingRecord = null, Action? backAction = null)
        {
            _member = member ?? new Beneficiary();
            _stagingRecord = stagingRecord;

            BackCommand = new RelayCommand(() =>
            {
                if (backAction != null)
                    backAction();
                else
                    NavigationService.Instance.NavigateTo(new ManageMembersView());
            });
            UpdateCommand = new RelayCommand(async () => await OpenForEditAsync(), () => CanUpdateMember);

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
        public bool CanUpdateMember => PermissionService.CanApproveWorkflow || Member.BenId == 0;
        public string ActionButtonText => Member.BenId == 0 ? "Link / Add" : "Update";
        public string ActionButtonIcon => Member.BenId == 0 ? "AccountPlus" : "Pencil";

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
            {
                if (_stagingRecord != null && _stagingRecord.LinkedEmpId.HasValue)
                {
                    IsLoading = true;
                    try
                    {
                        using var db = eSureHiDbContextFactory.Create();
                        var emp = await db.Employees
                            .Include(e => e.Department)
                            .FirstOrDefaultAsync(e => e.EmpId == _stagingRecord.LinkedEmpId.Value);
                        if (emp != null)
                        {
                            Member.Employee = emp;
                            Member.EmpId = emp.EmpId;
                            OnPropertyChanged(nameof(Barangay));
                            OnPropertyChanged(nameof(Address));
                            OnPropertyChanged(nameof(HasEmployee));
                            OnPropertyChanged(nameof(EmployeeNo));
                            OnPropertyChanged(nameof(EmployeeName));
                            OnPropertyChanged(nameof(EmployeePosition));
                            OnPropertyChanged(nameof(EmployeeDepartment));
                            OnPropertyChanged(nameof(EmployeeType));
                            OnPropertyChanged(nameof(EmployeeStatus));
                            OnPropertyChanged(nameof(EmployeeMobile));
                            OnPropertyChanged(nameof(EmployeeEmail));
                            OnPropertyChanged(nameof(EmployeeDateHired));
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Load employee details failed: {ex.GetBaseException().Message}";
                    }
                    finally
                    {
                        IsLoading = false;
                    }
                }
                return;
            }

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

        private async Task OpenForEditAsync()
        {
            if (!CanUpdateMember)
                return;

            if (Member.BenId == 0 && _stagingRecord != null)
            {
                var stagingVm = new BeneficiaryStagingViewModel
                {
                    SelectedSource = "CRS Master List",
                    SelectedRecord = _stagingRecord
                };
                NavigationService.Instance.NavigateTo(new BeneficiariesView(stagingVm, openInitialSearch: false));
                return;
            }

            var dialog = new Views.Admin.Dialogs.MemberEditDialog(Member)
            {
                Owner = App.ActiveShell,
                OnSaveSuccess = async () => await LoadAsync()
            };
            dialog.ShowDialog();
        }

        private string BuildAddress()
        {
            var employee = Member.Employee;
            if (employee is null)
            {
                if (_stagingRecord != null && !string.IsNullOrWhiteSpace(_stagingRecord.Address))
                    return _stagingRecord.Address.Trim();

                return NotSet;
            }

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
