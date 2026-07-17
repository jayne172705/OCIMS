using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using eSureHi.Views.Admin.Dialogs;

namespace eSureHi.ViewModels.Admin
{
    public class EmployeeFormViewModel : ObservableObject
    {
        // ── Mode ───────────────────────────────────────────────────────
        public bool IsEditMode { get; private set; }
        public string DialogTitle => IsEditMode ? "Edit Employee" : "New Employee";
        private int _editEmpId;

        // ── Departments ────────────────────────────────────────────────
        public ObservableCollection<Department> Departments { get; } = new();

        // ── Personal Tab ───────────────────────────────────────────────
        private string _employeeNo = string.Empty;
        private string _firstName = string.Empty;
        private string _middleName = string.Empty;
        private string _lastName = string.Empty;
        private string _suffix = string.Empty;
        private DateTime? _dateOfBirth;
        private string _gender = "Male";
        private string _civilStatus = "Single";
        private string _nationality = "Filipino";
        private string _photoPath = string.Empty;

        public string EmployeeNo { get => _employeeNo; set => SetProperty(ref _employeeNo, value); }
        public string FirstName { get => _firstName; set => SetProperty(ref _firstName, value); }
        public string MiddleName { get => _middleName; set => SetProperty(ref _middleName, value); }
        public string LastName { get => _lastName; set => SetProperty(ref _lastName, value); }
        public string Suffix { get => _suffix; set => SetProperty(ref _suffix, value); }
        public DateTime? DateOfBirth { get => _dateOfBirth; set => SetProperty(ref _dateOfBirth, value); }
        public string Gender { get => _gender; set => SetProperty(ref _gender, value); }
        public string CivilStatus { get => _civilStatus; set => SetProperty(ref _civilStatus, value); }
        public string Nationality { get => _nationality; set => SetProperty(ref _nationality, value); }
        public string PhotoPath
        {
            get => _photoPath;
            set
            {
                SetProperty(ref _photoPath, value);
                OnPropertyChanged(nameof(HasPhoto));
            }
        }
        public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath) && File.Exists(PhotoPath);

        // ── Contact Tab ────────────────────────────────────────────────
        private string _email = string.Empty;
        private string _phoneMobile = string.Empty;
        private string _phoneOffice = string.Empty;
        private string _addressLine1 = string.Empty;
        private string _addressLine2 = string.Empty;
        private string _city = string.Empty;
        private string _barangay = string.Empty;
        private string _province = string.Empty;
        private string _zipCode = string.Empty;

        public string Email { get => _email; set => SetProperty(ref _email, value); }
        public string PhoneMobile { get => _phoneMobile; set => SetProperty(ref _phoneMobile, value); }
        public string PhoneOffice { get => _phoneOffice; set => SetProperty(ref _phoneOffice, value); }
        public string AddressLine1 { get => _addressLine1; set => SetProperty(ref _addressLine1, value); }
        public string AddressLine2 { get => _addressLine2; set => SetProperty(ref _addressLine2, value); }
        public string City { get => _city; set => SetProperty(ref _city, value); }
        public string Barangay { get => _barangay; set => SetProperty(ref _barangay, value); }
        public string Province { get => _province; set => SetProperty(ref _province, value); }
        public string ZipCode { get => _zipCode; set => SetProperty(ref _zipCode, value); }

        // ── Beneficiaries Tab ──────────────────────────────────────────
        public ObservableCollection<Beneficiary> Beneficiaries { get; } = new();
        private Beneficiary? _selectedBeneficiary;
        public Beneficiary? SelectedBeneficiary
        {
            get => _selectedBeneficiary;
            set { SetProperty(ref _selectedBeneficiary, value); DeleteBeneficiaryCommand.RaiseCanExecuteChanged(); }
        }

        // ── Employment Tab ─────────────────────────────────────────────
        private Department? _selectedDepartment;
        private string _positionTitle = string.Empty;
        private string _employmentType = string.Empty;
        private decimal _monthlyContribution;
        private DateTime? _dateHired;
        private DateTime? _dateSeparated;
        private string _employmentStatus = "Active";

        public Department? SelectedDepartment
        {
            get => _selectedDepartment;
            set => SetProperty(ref _selectedDepartment, value);
        }
        public string PositionTitle { get => _positionTitle; set => SetProperty(ref _positionTitle, value); }
        public string EmploymentType
        {
            get => _employmentType;
            set
            {
                var previousDefault = GetDefaultMonthlyContribution(_employmentType);
                if (SetProperty(ref _employmentType, value) &&
                    (MonthlyContribution <= 0 || MonthlyContribution == previousDefault))
                {
                    MonthlyContribution = GetDefaultMonthlyContribution(value);
                }
            }
        }
        public decimal MonthlyContribution { get => _monthlyContribution; set => SetProperty(ref _monthlyContribution, value); }
        public DateTime? DateHired { get => _dateHired; set => SetProperty(ref _dateHired, value); }
        public DateTime? DateSeparated { get => _dateSeparated; set => SetProperty(ref _dateSeparated, value); }
        public string EmploymentStatus { get => _employmentStatus; set => SetProperty(ref _employmentStatus, value); }

        // ── Status / Error ─────────────────────────────────────────────
        private string _errorMessage = string.Empty;
        private bool _isBusy;

        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
        public bool IsBusy { get => _isBusy; set { SetProperty(ref _isBusy, value); SaveCommand.RaiseCanExecuteChanged(); } }

        // ── Static lists ───────────────────────────────────────────────
        public string[] GenderOptions { get; } = { "Male", "Female", "Other" };
        public string[] CivilStatusOptions { get; } = { "Single", "Married", "Widowed", "Separated" };
        public string[] EmploymentTypes { get; } = { "Job Order", "Casual", "Regular" };
        public string[] StatusOptions { get; } = { "Active", "Inactive", "Retired", "Resigned", "Terminated" };

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand BrowsePhotoCommand { get; }
        public RelayCommand ClearPhotoCommand { get; }
        public RelayCommand SelectCrsRecordCommand { get; }
        public RelayCommand AddBeneficiaryCommand { get; }
        public RelayCommand DeleteBeneficiaryCommand { get; }

        // ── Callback ───────────────────────────────────────────────────
        public Action? CloseAction { get; set; }
        public Action? OnSaveSuccess { get; set; }

        // ── Constructor ────────────────────────────────────────────────
        public EmployeeFormViewModel()
        {
            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsBusy);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
            BrowsePhotoCommand = new RelayCommand(BrowsePhoto);
            ClearPhotoCommand = new RelayCommand(() => PhotoPath = string.Empty);
            SelectCrsRecordCommand = new RelayCommand(async () => await SelectCrsRecordAsync());
            AddBeneficiaryCommand = new RelayCommand(async () => await AddBeneficiaryAsync());
            DeleteBeneficiaryCommand = new RelayCommand(DeleteBeneficiary, () => SelectedBeneficiary != null);
        }

        // ── Load for New ───────────────────────────────────────────────
        public async Task InitNewAsync()
        {
            IsEditMode = false;
            EmployeeNo = string.Empty;
            EmploymentType = string.Empty;
            MonthlyContribution = 0;
            DateHired = DateTime.Today;
            DateOfBirth = DateTime.Today.AddYears(-25);
            await LoadDepartmentsAsync();
        }

        // ── Load for Edit ──────────────────────────────────────────────
        public async Task InitEditAsync(int empId)
        {
            IsEditMode = true;
            _editEmpId = empId;
            await LoadDepartmentsAsync();

            using var db = eSureHiDbContextFactory.Create();
            var emp = await db.Employees
                .Include(e => e.Department)
                .Include(e => e.Beneficiaries)
                .FirstOrDefaultAsync(e => e.EmpId == empId);

            if (emp is null) return;

            EmployeeNo = emp.EmployeeNo;
            FirstName = emp.FirstName;
            MiddleName = emp.MiddleName ?? string.Empty;
            LastName = emp.LastName;
            Suffix = emp.Suffix ?? string.Empty;
            DateOfBirth = emp.DateOfBirth.HasValue
                                   ? emp.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)
                                   : null;
            Gender = emp.Gender ?? "Male";
            CivilStatus = emp.CivilStatus;
            Nationality = emp.Nationality;
            PhotoPath = emp.PhotoPath ?? string.Empty;

            Email = emp.Email ?? string.Empty;
            PhoneMobile = emp.PhoneMobile ?? string.Empty;
            PhoneOffice = emp.PhoneOffice ?? string.Empty;
            AddressLine1 = emp.AddressLine1 ?? string.Empty;
            AddressLine2 = emp.AddressLine2 ?? string.Empty;
            City = emp.City ?? string.Empty;
            Province = emp.Province ?? string.Empty;
            ZipCode = emp.ZipCode ?? string.Empty;

            PositionTitle = emp.PositionTitle ?? string.Empty;
            EmploymentType = emp.EmploymentType;
            var activeAssignment = await db.EmployeePolicies
                .Include(ep => ep.Policy)
                .Where(ep => ep.EmpId == emp.EmpId &&
                             ep.AssignmentStatus == "Active" &&
                             ep.Policy != null &&
                             ep.Policy.PolicyType == emp.EmploymentType)
                .OrderByDescending(ep => ep.UpdatedAt)
                .FirstOrDefaultAsync();
            MonthlyContribution = activeAssignment?.EmployeeShare ?? GetDefaultMonthlyContribution(emp.EmploymentType);
            DateHired = emp.DateHired.HasValue
                                   ? emp.DateHired.Value.ToDateTime(TimeOnly.MinValue)
                                   : null;
            DateSeparated = emp.DateSeparated.HasValue
                                   ? emp.DateSeparated.Value.ToDateTime(TimeOnly.MinValue)
                                   : null;
            EmploymentStatus = emp.EmploymentStatus;
            AddressLine1 = emp.AddressLine1 ?? string.Empty;
            AddressLine2 = emp.AddressLine2 ?? string.Empty;
            City = emp.City ?? string.Empty;
            Barangay = emp.Barangay ?? string.Empty;
            Province = emp.Province ?? string.Empty;
            ZipCode = emp.ZipCode ?? string.Empty;

            Beneficiaries.Clear();
            foreach (var b in emp.Beneficiaries)
                Beneficiaries.Add(b);

            SelectedDepartment = Departments.FirstOrDefault(d => d.DeptId == emp.DeptId);
        }

        // ── Load Departments ───────────────────────────────────────────
        private async Task LoadDepartmentsAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var depts = await db.Departments
                    .Where(d => d.IsActive)
                    .OrderBy(d => d.DeptName)
                    .ToListAsync();
                Departments.Clear();
                foreach (var d in depts)
                    Departments.Add(d);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load departments failed: {ex.Message}";
            }
        }

        // ── Browse Photo ───────────────────────────────────────────────
        private void BrowsePhoto()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Employee Photo",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp|All Files|*.*"
            };
            if (dlg.ShowDialog() == true)
                PhotoPath = dlg.FileName;
        }

        private async Task SelectCrsRecordAsync()
        {
            var dialog = new BeneficiaryStagingDialog(selectionOnly: true);
            if (App.ActiveShell is not null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;

            if (dialog.ShowDialog() != true || dialog.SelectedCrsRecord is null)
                return;

            ApplyCrsRecord(dialog.SelectedCrsRecord);
            ErrorMessage = string.Empty;
            await Task.CompletedTask;
        }

        private void ApplyCrsRecord(BeneficiaryStaging record)
        {
            EmployeeNo = BuildHouseholdEmployeeNo(record);

            var firstName = record.FirstName?.Trim();
            var lastName = record.LastName?.Trim();
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                (firstName, lastName) = SplitDisplayName(record.DisplayName);

            FirstName = string.IsNullOrWhiteSpace(firstName) ? FirstName : firstName;
            MiddleName = record.MiddleName?.Trim() ?? MiddleName;
            LastName = string.IsNullOrWhiteSpace(lastName) ? LastName : lastName;
            Gender = record.Sex?.Trim().ToUpperInvariant() switch
            {
                "MALE" or "M" => "Male",
                "FEMALE" or "F" => "Female",
                _ => Gender
            };

            if (!string.IsNullOrWhiteSpace(record.DateOfBirth) &&
                DateTime.TryParse(record.DateOfBirth, out var parsedDob))
            {
                DateOfBirth = parsedDob;
            }

            if (!string.IsNullOrWhiteSpace(record.MaritalStatus))
                CivilStatus = NormalizeCivilStatus(record.MaritalStatus);

            if (!string.IsNullOrWhiteSpace(record.Address))
            {
                AddressLine1 = record.Address.Trim();
                ApplyAddressParts(record.Address);
            }
        }

        private void ApplyAddressParts(string address)
        {
            var parts = address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return;

            if (string.IsNullOrWhiteSpace(Barangay))
                Barangay = parts[0];
            if (parts.Length > 1 && string.IsNullOrWhiteSpace(City))
                City = parts[^2];
            if (parts.Length > 2 && string.IsNullOrWhiteSpace(Province))
                Province = parts[^1];
        }

        private async Task AddBeneficiaryAsync()
        {
            var dialog = new BeneficiaryStagingDialog(selectionOnly: true);
            if (App.ActiveShell is not null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;

            if (dialog.ShowDialog() != true || dialog.SelectedCrsRecord is null)
                return;

            var record = dialog.SelectedCrsRecord;
            var beneficiaryId = record.BeneficiaryId?.Trim();
            var civilRegistryId = record.CivilRegistryId?.Trim();

            if (Beneficiaries.Any(b =>
                    (!string.IsNullOrWhiteSpace(beneficiaryId) && b.BeneficiaryId == beneficiaryId) ||
                    (!string.IsNullOrWhiteSpace(civilRegistryId) && b.CivilRegistryId == civilRegistryId)))
            {
                ErrorMessage = "This CRS beneficiary is already added to this employee.";
                return;
            }

            var firstName = record.FirstName?.Trim();
            var lastName = record.LastName?.Trim();
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                (firstName, lastName) = SplitDisplayName(record.DisplayName);
            }

            DateOnly? dob = null;
            if (!string.IsNullOrWhiteSpace(record.DateOfBirth) &&
                DateOnly.TryParse(record.DateOfBirth, out var parsedDob))
                dob = parsedDob;

            var gender = record.Sex?.Trim().ToUpperInvariant() switch
            {
                "MALE" or "M" => "Male",
                "FEMALE" or "F" => "Female",
                _ => "Other"
            };

            Beneficiaries.Add(new Beneficiary
            {
                BeneficiaryId = beneficiaryId,
                CivilRegistryId = civilRegistryId,
                FirstName = string.IsNullOrWhiteSpace(firstName) ? "N/A" : firstName,
                LastName = string.IsNullOrWhiteSpace(lastName) ? "N/A" : lastName,
                Relationship = "Other",
                DateOfBirth = dob,
                Gender = gender,
                IsActive = true,
                WorkflowStatus = WorkflowStatuses.Approved,
                StatusRemarks = "Selected from CRS master list.",
                CreatedAt = DateTime.Now
            });

            ErrorMessage = string.Empty;
            await Task.CompletedTask;
        }

        private void DeleteBeneficiary()
        {
            if (SelectedBeneficiary != null)
                Beneficiaries.Remove(SelectedBeneficiary);
        }

        // ── Validate ───────────────────────────────────────────────────
        private bool Validate()
        {
            if (string.IsNullOrWhiteSpace(EmployeeNo))
            { ErrorMessage = "Household/Resident ID is required. Select the person from CRS or enter the existing household ID."; return false; }
            if (string.IsNullOrWhiteSpace(FirstName))
            { ErrorMessage = "First name is required."; return false; }
            if (string.IsNullOrWhiteSpace(LastName))
            { ErrorMessage = "Last name is required."; return false; }
            if (string.IsNullOrWhiteSpace(Email))
            { ErrorMessage = "Email is required."; return false; }
            if (DateOfBirth is null)
            { ErrorMessage = "Date of birth is required."; return false; }
            if (string.IsNullOrWhiteSpace(EmploymentType))
            { ErrorMessage = "Employment type is required. Select Job Order, Casual, or Regular."; return false; }
            if (MonthlyContribution <= 0)
            { ErrorMessage = "Monthly contribution is required and must be greater than zero."; return false; }
            if (DateHired is null)
            { ErrorMessage = "Date hired is required."; return false; }
            ErrorMessage = string.Empty;
            return true;
        }

        // ── Save ───────────────────────────────────────────────────────
        private async Task SaveAsync()
        {
            if (!Validate()) return;
            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var employeeNo = EmployeeNo.Trim();

                bool duplicateEmployeeNo = await db.Employees
                    .AnyAsync(e => e.EmployeeNo == employeeNo &&
                                   (!IsEditMode || e.EmpId != _editEmpId));
                if (duplicateEmployeeNo)
                {
                    ErrorMessage = "This household/resident ID is already used by another employee.";
                    return;
                }

                if (IsEditMode)
                {
                    var emp = await db.Employees
                        .Include(e => e.Beneficiaries)
                        .FirstOrDefaultAsync(e => e.EmpId == _editEmpId);
                    if (emp is null) { ErrorMessage = "Employee not found."; return; }
                    MapToEntity(emp);
                    emp.UpdatedAt = DateTime.Now;

                    // Update Beneficiaries
                    db.Beneficiaries.RemoveRange(emp.Beneficiaries.Where(b => !Beneficiaries.Any(nb => nb.BenId == b.BenId && b.BenId != 0)));
                    foreach (var b in Beneficiaries)
                    {
                        if (b.BenId == 0) emp.Beneficiaries.Add(b);
                        else
                        {
                            var existing = emp.Beneficiaries.FirstOrDefault(x => x.BenId == b.BenId);
                            if (existing != null)
                            {
                                existing.FirstName = b.FirstName;
                                existing.LastName = b.LastName;
                                existing.Relationship = b.Relationship;
                                existing.DateOfBirth = b.DateOfBirth;
                                existing.Gender = b.Gender;
                                existing.IsPrimary = b.IsPrimary;
                                existing.RecipientsInsurance = b.RecipientsInsurance;
                                existing.CedulaNo = b.CedulaNo;
                                existing.Received = b.Received;
                                existing.Contribution = b.Contribution;
                            }
                        }
                    }

                    await db.SaveChangesAsync();
                    await EnsureMatchingPolicyAssignmentAsync(db, emp, MonthlyContribution);
                    await LinkEmployeeCrsStagingRecordAsync(db, emp);
                    await LinkCrsStagingRecordsAsync(db, emp);
                    // After edit employee save:
                    await AuditService.LogUpdate("employees", emp.EmpId,
                        $"Employee updated: {emp.FirstName} {emp.LastName}");
                }
                else
                {
                    // Check duplicate email
                    bool emailExists = await db.Employees
                        .AnyAsync(e => e.Email == Email.Trim());
                    if (emailExists)
                    { ErrorMessage = "An employee with this email already exists."; return; }

                    var emp = new Employee { CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now };
                    MapToEntity(emp);
                    emp.CreatedBy = AuthService.Instance.CurrentUser?.UserId;

                    // Add Beneficiaries for new employee
                    foreach (var b in Beneficiaries)
                    {
                        emp.Beneficiaries.Add(b);
                    }

                    db.Employees.Add(emp);
                    await db.SaveChangesAsync();
                    await EnsureMatchingPolicyAssignmentAsync(db, emp, MonthlyContribution);
                    await LinkEmployeeCrsStagingRecordAsync(db, emp);
                    await LinkCrsStagingRecordsAsync(db, emp);
                    
                    await AuditService.LogInsert("employees", emp.EmpId,
                        $"New employee registered: {emp.FirstName} {emp.LastName}");
                }

                OnSaveSuccess?.Invoke();
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Save failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Map VM → Entity ────────────────────────────────────────────
        private static async Task EnsureMatchingPolicyAssignmentAsync(eSureHiDbContext db, Employee employee, decimal monthlyContribution)
        {
            if (string.IsNullOrWhiteSpace(employee.EmploymentType))
                return;

            var matchingPolicy = await db.InsurancePolicies
                .Where(p => p.PolicyStatus == "Active" &&
                            p.PolicyType == employee.EmploymentType)
                .OrderBy(p => p.PolicyId)
                .FirstOrDefaultAsync();

            if (matchingPolicy is null)
                return;

            var assignments = await db.EmployeePolicies
                .Include(ep => ep.Policy)
                .Where(ep => ep.EmpId == employee.EmpId)
                .ToListAsync();

            foreach (var assignment in assignments.Where(ep =>
                         ep.Policy is not null &&
                         ep.AssignmentStatus == "Active" &&
                         ep.Policy.PolicyType != employee.EmploymentType))
            {
                assignment.AssignmentStatus = "Inactive";
                assignment.StatusRemarks = $"Auto-inactivated because employee is now {employee.EmploymentType}.";
                assignment.UpdatedAt = DateTime.Now;
            }

            var hasMatchingAssignment = assignments.Any(ep =>
                ep.PolicyId == matchingPolicy.PolicyId &&
                ep.AssignmentStatus == "Active");

            var coverageLimit = monthlyContribution * 100;
            foreach (var assignment in assignments.Where(ep =>
                         ep.PolicyId == matchingPolicy.PolicyId &&
                         ep.AssignmentStatus == "Active"))
            {
                assignment.EmployeeShare = monthlyContribution;
                assignment.EmployerShare = 0;
                assignment.CoverageLimit = coverageLimit;
                assignment.UpdatedAt = DateTime.Now;
            }

            if (!hasMatchingAssignment)
            {
                db.EmployeePolicies.Add(new EmployeePolicy
                {
                    EmpId = employee.EmpId,
                    PolicyId = matchingPolicy.PolicyId,
                    CoverageLimit = coverageLimit,
                    EmployeeShare = monthlyContribution,
                    EmployerShare = 0,
                    StartDate = DateOnly.FromDateTime(DateTime.Today),
                    AssignmentStatus = "Active",
                    Remarks = $"Auto-assigned from employee type: {employee.EmploymentType}.",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
            }

            await db.SaveChangesAsync();
        }

        private static async Task LinkCrsStagingRecordsAsync(eSureHiDbContext db, Employee employee)
        {
            var beneficiaries = await db.Beneficiaries
                .Where(b => b.EmpId == employee.EmpId)
                .ToListAsync();

            foreach (var beneficiary in beneficiaries)
            {
                var beneficiaryId = beneficiary.BeneficiaryId;
                var civilRegistryId = beneficiary.CivilRegistryId;
                if (string.IsNullOrWhiteSpace(beneficiaryId) && string.IsNullOrWhiteSpace(civilRegistryId))
                    continue;

                var stagingRecords = await db.BeneficiaryStaging
                    .Where(s =>
                        (!string.IsNullOrWhiteSpace(beneficiaryId) && s.BeneficiaryId == beneficiaryId) ||
                        (!string.IsNullOrWhiteSpace(civilRegistryId) && s.CivilRegistryId == civilRegistryId))
                    .ToListAsync();

                foreach (var staging in stagingRecords)
                {
                    staging.LinkStatus = "Linked";
                    staging.LinkedEmpId = employee.EmpId;
                    staging.LinkedBenId = beneficiary.BenId;
                }
            }

            await db.SaveChangesAsync();
        }

        private static async Task LinkEmployeeCrsStagingRecordAsync(eSureHiDbContext db, Employee employee)
        {
            var employeeNo = employee.EmployeeNo?.Trim();
            if (string.IsNullOrWhiteSpace(employeeNo))
                return;

            var stagingRecords = await db.BeneficiaryStaging
                .Where(s =>
                    (s.ResidentsId.HasValue && s.ResidentsId.Value.ToString() == employeeNo) ||
                    s.BeneficiaryId == employeeNo ||
                    s.CivilRegistryId == employeeNo)
                .ToListAsync();

            foreach (var staging in stagingRecords)
            {
                staging.LinkStatus = "Linked";
                staging.LinkedEmpId = employee.EmpId;
            }

            await db.SaveChangesAsync();
        }

        private void MapToEntity(Employee emp)
        {
            emp.EmployeeNo = EmployeeNo.Trim();
            emp.FirstName = FirstName.Trim();
            emp.MiddleName = string.IsNullOrWhiteSpace(MiddleName) ? null : MiddleName.Trim();
            emp.LastName = LastName.Trim();
            emp.Suffix = string.IsNullOrWhiteSpace(Suffix) ? null : Suffix.Trim();
            emp.DateOfBirth = DateOfBirth.HasValue
                                      ? DateOnly.FromDateTime(DateOfBirth.Value) : null;
            emp.Gender = Gender;
            emp.CivilStatus = CivilStatus;
            emp.Nationality = Nationality;
            emp.PhotoPath = string.IsNullOrWhiteSpace(PhotoPath) ? null : PhotoPath.Trim();
            emp.Email = Email.Trim();
            emp.PhoneMobile = string.IsNullOrWhiteSpace(PhoneMobile) ? null : PhoneMobile.Trim();
            emp.PhoneOffice = string.IsNullOrWhiteSpace(PhoneOffice) ? null : PhoneOffice.Trim();
            emp.AddressLine1 = string.IsNullOrWhiteSpace(AddressLine1) ? null : AddressLine1.Trim();
            emp.AddressLine2 = string.IsNullOrWhiteSpace(AddressLine2) ? null : AddressLine2.Trim();
            emp.City = string.IsNullOrWhiteSpace(City) ? null : City.Trim();
            emp.Barangay = string.IsNullOrWhiteSpace(Barangay) ? null : Barangay.Trim();
            emp.Province = string.IsNullOrWhiteSpace(Province) ? null : Province.Trim();
            emp.ZipCode = string.IsNullOrWhiteSpace(ZipCode) ? null : ZipCode.Trim();
            emp.DeptId = SelectedDepartment?.DeptId;
            emp.PositionTitle = string.IsNullOrWhiteSpace(PositionTitle) ? null : PositionTitle.Trim();
            emp.EmploymentType = EmploymentType.Trim();
            emp.DateHired = DateHired.HasValue
                                      ? DateOnly.FromDateTime(DateHired.Value) : null;
            emp.DateSeparated = DateSeparated.HasValue
                                      ? DateOnly.FromDateTime(DateSeparated.Value) : null;
            emp.EmploymentStatus = EmploymentStatus;
        }

        // ── Household / Resident ID ────────────────────────────────────
        private static string BuildHouseholdEmployeeNo(BeneficiaryStaging record)
        {
            if (!string.IsNullOrWhiteSpace(record.BeneficiaryId))
                return record.BeneficiaryId.Trim();

            if (!string.IsNullOrWhiteSpace(record.CivilRegistryId))
                return record.CivilRegistryId.Trim();

            return record.ResidentsId?.ToString() ?? string.Empty;
        }

        private static string NormalizeCivilStatus(string value)
        {
            var normalized = value.Trim().ToUpperInvariant();
            return normalized switch
            {
                "MARRIED" or "M" => "Married",
                "WIDOWED" or "W" => "Widowed",
                "SEPARATED" => "Separated",
                _ => "Single"
            };
        }

        private static decimal GetDefaultMonthlyContribution(string? employmentType) =>
            employmentType?.Trim() switch
            {
                "Job Order" => 50m,
                "Casual" => 100m,
                "Regular" => 200m,
                _ => 0m
            };

        private static (string FirstName, string LastName) SplitDisplayName(string? displayName)
        {
            var value = displayName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value))
                return ("N/A", "N/A");

            if (value.Contains(','))
            {
                var parts = value.Split(',', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                return (parts.Length > 1 ? parts[1] : parts[0], parts[0]);
            }

            var nameParts = value.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return (nameParts[0], nameParts.Length > 1 ? nameParts[1] : "N/A");
        }
    }
}
