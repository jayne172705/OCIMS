using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using eSureHi.Models;
using eSureHi.Data;
using eSureHi.Services;
using eSureHi.ViewModels.Admin;
using eSureHi.Helpers;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class RegisterPrimaryDialog : Window, INotifyPropertyChanged
    {
        private readonly BeneficiaryStaging _resident;

        // Form Fields
        private string _selectedMemberType = "New Member";
        private DateTime _dateRegistered = DateTime.Today;
        private SourceFund? _selectedAssignedGroup;
        private InsurancePolicy? _selectedPaymentPlan;
        private string _contactNumber = string.Empty;
        private string _emailAddress = string.Empty;

        // Dependents Fields
        private bool _enableOtherResidents;
        private string _otherResidentsSearchText = string.Empty;
        private BeneficiaryStaging? _selectedOtherResident;
        private decimal _totalPayment;

        public event PropertyChangedEventHandler? PropertyChanged;

        // Prefilled Data properties
        public string ResidentName => _resident.FullName ?? _resident.DisplayName ?? "Unknown";
        public string ResidentDob => _resident.DateOfBirth ?? "Not Specified";
        public string ResidentSex => _resident.Sex ?? "Not Specified";
        public string ResidentBarangay
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_resident.Address)) return "Not Specified";
                var parts = _resident.Address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                return parts.Length > 0 ? parts[0] : "Not Specified";
            }
        }
        public string ResidentFamilyId => _resident.DemographicFamilyId ?? "None";
        public string ResidentId => _resident.BeneficiaryId ?? "None";

        // Collections
        public ObservableCollection<string> MemberTypeOptions { get; } = new() { "New Member", "Old Member" };
        public ObservableCollection<SourceFund> AssignedGroupOptions { get; } = new();
        public ObservableCollection<InsurancePolicy> PaymentPlanOptions { get; } = new();
        public ObservableCollection<FamilyMemberDisplay> HouseholdFamilyMembers { get; } = new();
        public ObservableCollection<BeneficiaryStaging> OtherResidents { get; } = new();
        
        // New Dependents Collections
        public ObservableCollection<HouseholdDependentRow> HouseholdDependentRows { get; } = new();
        public ObservableCollection<DependentRow> AddedOtherDependents { get; } = new();

        // Commands
        public ICommand AddHouseholdDependentRowCommand { get; }
        public ICommand RemoveHouseholdDependentRowCommand { get; }
        public ICommand AddOtherDependentCommand { get; }
        public ICommand RemoveOtherDependentCommand { get; }
        public ICommand RegisterCommand { get; }

        public string SelectedMemberType
        {
            get => _selectedMemberType;
            set { if (SetProperty(ref _selectedMemberType, value)) OnPropertyChanged(); }
        }

        public DateTime DateRegistered
        {
            get => _dateRegistered;
            set { if (SetProperty(ref _dateRegistered, value)) OnPropertyChanged(); }
        }

        public SourceFund? SelectedAssignedGroup
        {
            get => _selectedAssignedGroup;
            set { if (SetProperty(ref _selectedAssignedGroup, value)) OnPropertyChanged(); }
        }

        public InsurancePolicy? SelectedPaymentPlan
        {
            get => _selectedPaymentPlan;
            set
            {
                if (SetProperty(ref _selectedPaymentPlan, value))
                {
                    OnPropertyChanged();
                    UpdateDefaultContributions();
                }
            }
        }

        public string ContactNumber
        {
            get => _contactNumber;
            set { if (SetProperty(ref _contactNumber, value)) OnPropertyChanged(); }
        }

        public string EmailAddress
        {
            get => _emailAddress;
            set { if (SetProperty(ref _emailAddress, value)) OnPropertyChanged(); }
        }

        public bool EnableOtherResidents
        {
            get => _enableOtherResidents;
            set
            {
                if (SetProperty(ref _enableOtherResidents, value))
                {
                    OnPropertyChanged();
                    if (!_enableOtherResidents)
                    {
                        OtherResidentsSearchText = string.Empty;
                        OtherResidents.Clear();
                        AddedOtherDependents.Clear();
                        UpdateTotalPayment();
                    }
                }
            }
        }

        public string OtherResidentsSearchText
        {
            get => _otherResidentsSearchText;
            set
            {
                if (SetProperty(ref _otherResidentsSearchText, value))
                {
                    OnPropertyChanged();
                    _ = SearchOtherResidentsAsync(value);
                }
            }
        }

        public BeneficiaryStaging? SelectedOtherResident
        {
            get => _selectedOtherResident;
            set { if (SetProperty(ref _selectedOtherResident, value)) OnPropertyChanged(); }
        }

        public decimal TotalPayment
        {
            get => _totalPayment;
            set { if (SetProperty(ref _totalPayment, value)) OnPropertyChanged(); }
        }

        public RegisterPrimaryDialog(BeneficiaryStaging resident)
        {
            InitializeComponent();
            _resident = resident;
            DataContext = this;

            AddHouseholdDependentRowCommand = new RelayCommand(AddHouseholdDependentRow);
            RemoveHouseholdDependentRowCommand = new RelayCommand<HouseholdDependentRow>(RemoveHouseholdDependentRow);
            AddOtherDependentCommand = new RelayCommand(AddOtherDependent);
            RemoveOtherDependentCommand = new RelayCommand<DependentRow>(RemoveOtherDependent);
            RegisterCommand = new RelayCommand(async () => await RegisterAsync());

            ContactNumber = string.Empty;
            EmailAddress = string.Empty;

            _ = LoadInitialDataAsync();
        }

        private async Task LoadInitialDataAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                // 1. Load active source funds (Assigned Groups)
                var funds = await db.SourceFunds
                    .Where(f => f.Status == "Active" && f.FundType == "Employee Track")
                    .OrderBy(f => f.FundName)
                    .ToListAsync();
                AssignedGroupOptions.Clear();
                AssignedGroupOptions.Add(new SourceFund { SourceFundId = 0, FundName = "— Select Group —" });
                foreach (var f in funds) AssignedGroupOptions.Add(f);

                // 2. Load active insurance policies (Payment Plans)
                var policies = await db.InsurancePolicies
                    .Where(p => p.PolicyStatus == "Active")
                    .OrderBy(p => p.PolicyName)
                    .ToListAsync();
                PaymentPlanOptions.Clear();
                foreach (var p in policies) PaymentPlanOptions.Add(p);

                SelectedAssignedGroup = AssignedGroupOptions.First();

                if (PaymentPlanOptions.Any())
                {
                    SelectedPaymentPlan = PaymentPlanOptions.FirstOrDefault(p => 
                        p.PolicyType.Equals(_resident.DemographicFamilyRole, StringComparison.OrdinalIgnoreCase))
                        ?? PaymentPlanOptions.FirstOrDefault(p => p.PolicyType.Contains("Job Order", StringComparison.OrdinalIgnoreCase))
                        ?? PaymentPlanOptions.First();
                }

                // 3. Load family members sharing demographic family ID
                await LoadHouseholdFamilyMembersAsync();

                // Prefill one empty dependent row if household members exist
                if (HouseholdFamilyMembers.Any())
                {
                    AddHouseholdDependentRow();
                }

                UpdateTotalPayment();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load options: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadHouseholdFamilyMembersAsync()
        {
            if (string.IsNullOrWhiteSpace(_resident.DemographicFamilyId))
                return;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var familyMembers = await db.CrsBeneficiaryCache
                    .Where(c => c.FamilyId == _resident.DemographicFamilyId)
                    .ToListAsync();

                var civilRegistryIds = familyMembers
                    .Where(m => !string.IsNullOrWhiteSpace(m.CivilRegistryId))
                    .Select(m => m.CivilRegistryId)
                    .ToList();

                var activeBeneficiaries = await db.Beneficiaries
                    .Where(b => b.IsActive && civilRegistryIds.Contains(b.CivilRegistryId))
                    .Select(b => new { b.CivilRegistryId, b.IsPrimary })
                    .ToListAsync();

                HouseholdFamilyMembers.Clear();
                foreach (var member in familyMembers.OrderBy(m => m.IsHouseholdHead ? 0 : 1).ThenBy(m => m.FullName))
                {
                    var matchesCivilRegistryId = !string.IsNullOrWhiteSpace(member.CivilRegistryId) && 
                                                !string.IsNullOrWhiteSpace(_resident.CivilRegistryId) && 
                                                member.CivilRegistryId == _resident.CivilRegistryId;

                    var matchesBeneficiaryId = !string.IsNullOrWhiteSpace(member.BeneficiaryId) && 
                                               !string.IsNullOrWhiteSpace(_resident.BeneficiaryId) && 
                                               member.BeneficiaryId == _resident.BeneficiaryId;

                    if (matchesCivilRegistryId || matchesBeneficiaryId)
                        continue;

                    var status = "Not Registered";
                    var bg = "#F1F5F9";
                    var fg = "#64748B";

                    if (!string.IsNullOrWhiteSpace(member.CivilRegistryId))
                    {
                        var ben = activeBeneficiaries.FirstOrDefault(b => b.CivilRegistryId == member.CivilRegistryId);
                        if (ben != null)
                        {
                            if (ben.IsPrimary)
                            {
                                status = "Registered Member";
                                bg = "#DCFCE7";
                                fg = "#166534";
                            }
                            else
                            {
                                status = "Dependent";
                                bg = "#DBEAFE";
                                fg = "#1E40AF";
                            }
                        }
                    }

                    HouseholdFamilyMembers.Add(new FamilyMemberDisplay
                    {
                        FullName = member.FullName ?? "Unknown",
                        FamilyRole = string.IsNullOrWhiteSpace(member.FamilyRole) ? "MEMBER" : member.FamilyRole.ToUpper(),
                        RegistrationStatus = status,
                        RegistrationStatusBackground = bg,
                        RegistrationStatusForeground = fg,
                        CivilRegistryId = member.CivilRegistryId ?? string.Empty,
                        BeneficiaryId = member.BeneficiaryId ?? string.Empty
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load family members: {ex.Message}");
            }
        }

        private async Task SearchOtherResidentsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                OtherResidents.Clear();
                return;
            }

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var list = await db.BeneficiaryStaging
                    .Where(s => s.LinkStatus != "Linked" &&
                                s.StagingId != _resident.StagingId &&
                                s.DemographicFamilyId != _resident.DemographicFamilyId &&
                                (EF.Functions.Like(s.FirstName, $"%{query}%") ||
                                 EF.Functions.Like(s.LastName, $"%{query}%") ||
                                 EF.Functions.Like(s.DisplayName, $"%{query}%")))
                    .Take(15)
                    .ToListAsync();

                OtherResidents.Clear();
                foreach (var item in list)
                {
                    OtherResidents.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to search other residents: {ex.Message}");
            }
        }

        private void AddHouseholdDependentRow()
        {
            HouseholdDependentRows.Add(new HouseholdDependentRow(this, HouseholdFamilyMembers));
        }

        private void RemoveHouseholdDependentRow(HouseholdDependentRow? row)
        {
            if (row != null)
            {
                HouseholdDependentRows.Remove(row);
                UpdateTotalPayment();
            }
        }

        public bool IsMemberAlreadySelected(FamilyMemberDisplay member, HouseholdDependentRow currentRow)
        {
            return HouseholdDependentRows.Any(r => r != currentRow && r.SelectedMember == member);
        }

        private void AddOtherDependent()
        {
            if (SelectedOtherResident is null)
                return;

            if (AddedOtherDependents.Any(d => d.StagingRecord.StagingId == SelectedOtherResident.StagingId))
            {
                MessageBox.Show("This resident has already been added as a dependent.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dep = new DependentRow
            {
                FullName = SelectedOtherResident.FullName ?? SelectedOtherResident.DisplayName ?? "Unknown",
                Relationship = MapRoleToRelationship(SelectedOtherResident.DemographicFamilyRole),
                Contribution = GetDefaultMonthlyContribution(SelectedPaymentPlan?.PolicyType),
                StagingRecord = SelectedOtherResident
            };

            AddedOtherDependents.Add(dep);
            UpdateTotalPayment();
        }

        private void RemoveOtherDependent(DependentRow? row)
        {
            if (row != null)
            {
                AddedOtherDependents.Remove(row);
                UpdateTotalPayment();
            }
        }

        private void UpdateDefaultContributions()
        {
            var primaryContribution = GetDefaultMonthlyContribution(SelectedPaymentPlan?.PolicyType);
            foreach (var dep in AddedOtherDependents)
            {
                dep.Contribution = primaryContribution;
            }
            UpdateTotalPayment();
        }

        public void UpdateTotalPayment()
        {
            var primaryContribution = GetDefaultMonthlyContribution(SelectedPaymentPlan?.PolicyType);
            var hhCount = HouseholdDependentRows.Count(r => r.SelectedMember != null);
            var otherCount = AddedOtherDependents.Count;
            TotalPayment = primaryContribution + (hhCount + otherCount) * primaryContribution;
        }

        private decimal GetDefaultMonthlyContribution(string? policyType)
        {
            if (string.IsNullOrWhiteSpace(policyType)) return 100.00m;
            return policyType.ToUpperInvariant() switch
            {
                "REGULAR" => 200.00m,
                "CASUAL" => 150.00m,
                "JOB ORDER" => 100.00m,
                "BARANGAY OFFICIALS" => 50.00m,
                "SENIOR CITIZEN" => 0.00m,
                _ => 100.00m
            };
        }

        private BeneficiaryStaging GetOrCreateStagingRecordForFamilyMember(
            eSureHiDbContext db,
            FamilyMemberDisplay member)
        {
            var existing = db.BeneficiaryStaging.FirstOrDefault(s =>
                (!string.IsNullOrEmpty(member.BeneficiaryId) && s.BeneficiaryId == member.BeneficiaryId) ||
                (!string.IsNullOrEmpty(member.CivilRegistryId) && s.CivilRegistryId == member.CivilRegistryId));

            if (existing != null)
                return existing;

            return new BeneficiaryStaging
            {
                BeneficiaryId = member.BeneficiaryId,
                CivilRegistryId = member.CivilRegistryId,
                FullName = member.FullName,
                FirstName = member.FullName,
                LastName = "N/A",
                DemographicFamilyRole = member.FamilyRole,
                DemographicFamilyId = _resident.DemographicFamilyId,
                Address = _resident.Address,
                LinkStatus = "Unlinked"
            };
        }

        private async Task RegisterAsync()
        {
            if (SelectedAssignedGroup is null || SelectedAssignedGroup.SourceFundId == 0)
            {
                MessageBox.Show("Please select an Assigned Group.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (SelectedPaymentPlan is null)
            {
                MessageBox.Show("Please select a Payment Plan.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(ContactNumber))
            {
                MessageBox.Show("Please enter a Contact Number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Explicit registration transactions cannot use the MySQL retry
                // strategy configured for ordinary remote reads/writes.
                using var db = eSureHiDbContextFactory.CreateWithoutRetry();
                using var transaction = await db.Database.BeginTransactionAsync();

                DateOnly? dob = null;
                if (DateOnly.TryParse(_resident.DateOfBirth, out var parsedDob))
                {
                    dob = parsedDob;
                }

                // Look up default active department
                var defaultDept = await db.Departments
                    .FirstOrDefaultAsync(d => d.IsActive && EF.Functions.Like(d.DeptName, "%Mayor%"))
                    ?? await db.Departments.FirstOrDefaultAsync(d => d.IsActive);
                int? deptId = defaultDept?.DeptId;

                // 1. Create the Employee (Primary member)
                var employeeNo = _resident.BeneficiaryId ?? $"IMS-EMP-{Guid.NewGuid().ToString().Substring(0, 8)}";
                var email = string.IsNullOrWhiteSpace(EmailAddress)
                    ? CreatePendingEmailAddress(employeeNo)
                    : EmailAddress.Trim();

                var employee = new Employee
                {
                    EmployeeNo = employeeNo,
                    FirstName = _resident.FirstName ?? _resident.DisplayName ?? "N/A",
                    LastName = _resident.LastName ?? "N/A",
                    DateOfBirth = dob,
                    Gender = NormalizeGender(_resident.Sex),
                    PhoneMobile = ContactNumber.Trim(),
                    // The IMS employee table requires a unique email even though this
                    // form permits a blank value.  Keep the member registered by using
                    // a unique internal placeholder until a real email is provided.
                    Email = email,
                    Barangay = ResidentBarangay,
                    City = "Sulop",
                    Province = "Davao del Sur",
                    DeptId = deptId,
                    EmploymentType = MapEmploymentType(SelectedAssignedGroup.FundName),
                    DateHired = DateOnly.FromDateTime(DateTime.Today),
                    EmploymentStatus = "Active",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    CreatedBy = AuthService.Instance.CurrentUser?.UserId
                };

                db.Employees.Add(employee);
                await db.SaveChangesAsync();

                // 2. Create the EmployeePolicy assignment
                var contributionAmt = GetDefaultMonthlyContribution(SelectedPaymentPlan.PolicyType);
                var ep = new EmployeePolicy
                {
                    EmpId = employee.EmpId,
                    PolicyId = SelectedPaymentPlan.PolicyId,
                    CoverageLimit = contributionAmt * 100,
                    EmployeeShare = contributionAmt,
                    EmployerShare = 0,
                    StartDate = DateOnly.FromDateTime(DateTime.Today),
                    AssignmentStatus = "Active",
                    Remarks = $"Auto-assigned during insurance registration.",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                db.EmployeePolicies.Add(ep);

                // 3. Create the primary member.  The remote IMS relationship enum
                // does not include "Self"; IsPrimary is the authoritative marker.
                var primaryBen = new Beneficiary
                {
                    EmpId = employee.EmpId,
                    BeneficiaryId = employee.EmployeeNo,
                    CivilRegistryId = _resident.CivilRegistryId,
                    FirstName = employee.FirstName,
                    LastName = employee.LastName,
                    Relationship = "Other",
                    DateOfBirth = employee.DateOfBirth,
                    Gender = employee.Gender,
                    IsPrimary = true,
                    Received = true,
                    Contribution = contributionAmt,
                    SourceOfFunds = SelectedPaymentPlan.PolicyType,
                    WorkflowStatus = "Approved",
                    StatusRemarks = "Registered as Primary Member",
                    IsActive = true,
                    IsAdminConfirmed = true,
                    CreatedAt = DateTime.Now
                };
                db.Beneficiaries.Add(primaryBen);
                await db.SaveChangesAsync();

                // Link primary staging record
                var primaryStaging = await db.BeneficiaryStaging.FindAsync(_resident.StagingId);
                if (primaryStaging != null)
                {
                    primaryStaging.LinkStatus = "Linked";
                    primaryStaging.LinkedEmpId = employee.EmpId;
                    primaryStaging.LinkedBenId = primaryBen.BenId;
                    await db.SaveChangesAsync();
                }

                // Gather and unify all dependents to save
                var dependentsToSave = new List<DependentRow>();

                foreach (var row in HouseholdDependentRows)
                {
                    if (row.SelectedMember != null)
                    {
                        var staging = GetOrCreateStagingRecordForFamilyMember(db, row.SelectedMember);
                        dependentsToSave.Add(new DependentRow
                        {
                            FullName = row.SelectedMember.FullName,
                            Relationship = MapRoleToRelationship(row.SelectedMember.FamilyRole),
                            Contribution = contributionAmt,
                            StagingRecord = staging
                        });
                    }
                }

                foreach (var otherDep in AddedOtherDependents)
                {
                    dependentsToSave.Add(otherDep);
                }

                // 4. Create Beneficiaries for all added dependents
                foreach (var dep in dependentsToSave)
                {
                    // Ensure the staging record exists in db
                    if (dep.StagingRecord.StagingId == 0)
                    {
                        db.BeneficiaryStaging.Add(dep.StagingRecord);
                        await db.SaveChangesAsync();
                    }

                    DateOnly? depDob = null;
                    if (DateOnly.TryParse(dep.StagingRecord.DateOfBirth, out var parsedDepDob))
                    {
                        depDob = parsedDepDob;
                    }

                    var beneficiary = new Beneficiary
                    {
                        EmpId = employee.EmpId,
                        BeneficiaryId = dep.StagingRecord.BeneficiaryId ?? $"IMS-BEN-{Guid.NewGuid().ToString().Substring(0, 8)}",
                        CivilRegistryId = dep.StagingRecord.CivilRegistryId,
                        FirstName = dep.StagingRecord.FirstName ?? dep.StagingRecord.DisplayName ?? "N/A",
                        LastName = dep.StagingRecord.LastName ?? "N/A",
                        Relationship = dep.Relationship,
                        DateOfBirth = depDob,
                        Gender = NormalizeGender(dep.StagingRecord.Sex),
                        IsPrimary = false,
                        Received = true,
                        Contribution = dep.Contribution,
                        SourceOfFunds = SelectedPaymentPlan.PolicyType,
                        WorkflowStatus = "Approved",
                        StatusRemarks = "Added during primary member registration",
                        IsActive = true,
                        IsAdminConfirmed = true,
                        CreatedAt = DateTime.Now
                    };

                    db.Beneficiaries.Add(beneficiary);
                    await db.SaveChangesAsync();

                    // Link dependent staging record
                    var depStaging = await db.BeneficiaryStaging.FindAsync(dep.StagingRecord.StagingId);
                    if (depStaging != null)
                    {
                        depStaging.LinkStatus = "Linked";
                        depStaging.LinkedEmpId = employee.EmpId;
                        depStaging.LinkedBenId = beneficiary.BenId;
                        await db.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();

                await AuditService.LogInsert("employees", employee.EmpId,
                    $"Registered primary member via insurance registration: {employee.FullName} with {dependentsToSave.Count} dependents");

                MessageBox.Show("Primary member and dependents successfully registered for insurance.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to register: {GetErrorDetails(ex)}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static string MapRoleToRelationship(string? role)
        {
            if (string.IsNullOrWhiteSpace(role)) return "Other";
            return role.ToUpperInvariant() switch
            {
                "SPOUSE" => "Spouse",
                "SON" or "DAUGHTER" => "Child",
                "CHILD" or "CHILDREN" => "Child",
                "FATHER" or "MOTHER" or "PARENT" => "Parent",
                "BROTHER" or "SISTER" or "SIBLING" => "Sibling",
                _ => "Other"
            };
        }

        private static string NormalizeGender(string? gender) => gender?.Trim().ToUpperInvariant() switch
        {
            "M" or "MALE" => "Male",
            "F" or "FEMALE" => "Female",
            "O" or "OTHER" => "Other",
            _ => "Other"
        };

        private static string MapEmploymentType(string? groupName) => groupName?.Trim().ToUpperInvariant() switch
        {
            "REGULAR" => "Regular",
            "PART-TIME" or "PART TIME" => "Part-time",
            "PROBATIONARY" => "Probationary",
            "CONTRACTUAL" or "CONTRACT" or "JOB ORDER" or "CASUAL" => "Contractual",
            _ => "Regular"
        };

        private static string CreatePendingEmailAddress(string employeeNo)
        {
            var safeId = new string(employeeNo
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
                .ToArray())
                .Trim('-');
            return $"{safeId}@pending.esurehi.local";
        }

        private static string GetErrorDetails(Exception exception)
        {
            var messages = new List<string>();
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (!string.IsNullOrWhiteSpace(current.Message) && !messages.Contains(current.Message))
                    messages.Add(current.Message);
            }

            return string.Join(Environment.NewLine, messages);
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class HouseholdDependentRow : INotifyPropertyChanged
    {
        private readonly RegisterPrimaryDialog _parent;
        private FamilyMemberDisplay? _selectedMember;

        public FamilyMemberDisplay? SelectedMember
        {
            get => _selectedMember;
            set
            {
                if (_selectedMember != value)
                {
                    if (value != null && _parent.IsMemberAlreadySelected(value, this))
                    {
                        MessageBox.Show("This family member is already selected in another dropdown.", "Duplicate Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                        OnPropertyChanged();
                        return;
                    }
                    _selectedMember = value;
                    OnPropertyChanged();
                    _parent.UpdateTotalPayment();
                }
            }
        }

        public ObservableCollection<FamilyMemberDisplay> Options { get; }

        public HouseholdDependentRow(RegisterPrimaryDialog parent, ObservableCollection<FamilyMemberDisplay> options)
        {
            _parent = parent;
            Options = options;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class DependentRow : INotifyPropertyChanged
    {
        private string _fullName = string.Empty;
        private string _relationship = "Other";
        private decimal _contribution;

        public string FullName 
        { 
            get => _fullName; 
            set { _fullName = value; OnPropertyChanged(); } 
        }

        public string Relationship 
        { 
            get => _relationship; 
            set { _relationship = value; OnPropertyChanged(); } 
        }

        public decimal Contribution 
        { 
            get => _contribution; 
            set { _contribution = value; OnPropertyChanged(); } 
        }

        public BeneficiaryStaging StagingRecord { get; set; } = null!;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
