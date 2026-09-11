using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using eSureHi.Views.Admin.Dialogs;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Windows;
using SystemClaim = System.Security.Claims.Claim;
using Claim = eSureHi.Models.Claim;

namespace eSureHi.ViewModels.Admin
{
    public class ClaimFormViewModel : ObservableObject
    {
        // ── Mode ───────────────────────────────────────────────────────
        public bool IsEditMode { get; private set; }
        public string DialogTitle => IsEditMode ? "Edit Insurance Claim" : "New Insurance Claim";
        private int _editClaimId;
        private int? _lockedBeneficiaryId;
        public bool IsBeneficiaryLocked => _lockedBeneficiaryId.HasValue;
        public bool CanChangeClaimant => !IsBeneficiaryLocked;

        // ── Applicant / Household info (beneficiary self-service path) ──
        public string ApplicantName { get; private set; } = string.Empty;
        public bool IsHouseholdHead { get; private set; }
        public string FamilyRole => IsHouseholdHead ? "Household Head" : "Dependent Member";
        public string HouseholdStatusBadge => IsHouseholdHead ? "Registered Member" : "Dependent";
        public ObservableCollection<FamilyMemberDisplay> FamilyMembers { get; } = new();

        private decimal _availableFundBalance;
        public decimal AvailableFundBalance
        {
            get => _availableFundBalance;
            private set => SetProperty(ref _availableFundBalance, value);
        }

        public string[] Hospitals { get; } =
        {
            "-- Select Hospital --",
            "Sulop Medical Clinic",
            "Davao del Sur Provincial Hospital",
            "Medical Center of Digos Cooperative (MCDC)",
            "Digos Doctor's Hospital Inc.",
            "South Davao Medical Specialist's Hospital Inc.",
            "Baron-Yee Hospital",
            "Gonzales-Maranan Medical Center, Inc.",
            "St. Dominic Hospital of Digos, Inc."
        };

        public string HospitalBillFileName => Documents.FirstOrDefault(d => d.DocType == "Hospital Bill")?.FileName ?? "No file chosen";
        public string OfficialReceiptFileName => Documents.FirstOrDefault(d => d.DocType == "Official Receipt")?.FileName ?? "No file chosen";

        // ── Step 1: Employee Selection ─────────────────────────────────
        private ObservableCollection<Employee> _allEmployees = new();
        private readonly Task _initialEmployeesLoadTask;
        public ObservableCollection<Employee> FilteredEmployees { get; } = new();

        private string _employeeSearch = string.Empty;
        public string EmployeeSearch
        {
            get => _employeeSearch;
            set { SetProperty(ref _employeeSearch, value); FilterEmployees(); }
        }

        private Employee? _selectedEmployee;
        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                SetProperty(ref _selectedEmployee, value);
                OnPropertyChanged(nameof(HasEmployee));
                SelectedEmployeePolicy = null;
                SelectedBeneficiary = null;
                OnPropertyChanged(nameof(HasPolicies));
                OnPropertyChanged(nameof(HasBeneficiaries));
                OnPropertyChanged(nameof(PolicyEmptyMessage));
                OnPropertyChanged(nameof(BeneficiaryEmptyMessage));
                _ = LoadPoliciesForEmployeeAsync();
            }
        }
        public bool HasEmployee => SelectedEmployee is not null;

        // ── Step 2: Policy + Basic Info ────────────────────────────────
        public ObservableCollection<EmployeePolicy> EmployeePolicies { get; } = new();
        public ObservableCollection<Beneficiary> Beneficiaries { get; } = new();

        private EmployeePolicy? _selectedEmployeePolicy;
        public EmployeePolicy? SelectedEmployeePolicy
        {
            get => _selectedEmployeePolicy;
            set
            {
                if (SetProperty(ref _selectedEmployeePolicy, value))
                {
                    OnPropertyChanged(nameof(MonthlyContribution));
                    OnPropertyChanged(nameof(MaxClaimAmount));
                    OnPropertyChanged(nameof(DailyAllowanceRate));
                    OnPropertyChanged(nameof(CoveredAllowanceDays));
                    OnPropertyChanged(nameof(DailyAllowanceAmount));
                    OnPropertyChanged(nameof(ClaimLimitText));
                    RefreshDailyAllowance();
                }
            }
        }
        public bool HasPolicies => EmployeePolicies.Count > 0;
        public decimal MonthlyContribution => SelectedEmployeePolicy?.EmployeeShare ?? GetDefaultMonthlyContribution(SelectedEmployee?.EmploymentType);
        public decimal MaxClaimAmount => SelectedEmployeePolicy?.CoverageLimit > 0 ? SelectedEmployeePolicy.CoverageLimit : (MonthlyContribution * 100);

        private static decimal GetDefaultMonthlyContribution(string? employmentType) =>
            employmentType?.Trim() switch
            {
                "Job Order" => 50m,
                "Casual" => 100m,
                "Regular" => 200m,
                _ => 0m
            };

        // ── Claim-breakdown rules (reference layout) ───────────────────
        public const decimal DailyAllowancePerDay = 750m;   // PHP 750 / day
        public const int MaxAllowanceDays = 5;              // capped at 5 days
        public const decimal ExcessBillCap = 20000m;        // PHP 20,000 max
        public const decimal DiagnosticsCoverageRate = 0.5m; // 50% covered
        public const decimal DiagnosticsCap = 20000m;       // PHP 20,000 max

        public decimal DailyAllowanceRate => DailyAllowancePerDay;
        public int CoveredAllowanceDays => Math.Min(Math.Max(AdmissionDays, 0), MaxAllowanceDays);
        public decimal DailyAllowanceAmount => DailyAllowanceRate * CoveredAllowanceDays;

        // Total Excess Bill — covered up to the PHP 20,000 cap.
        public decimal ExcessBillCovered => Math.Min(Math.Max(ExcessBillAmount, 0), ExcessBillCap);
        // Outside Diagnostics — 50% covered, capped at PHP 20,000.
        public decimal DiagnosticsCovered =>
            Math.Min(Math.Max(OutsideDiagnosticsAmount, 0) * DiagnosticsCoverageRate, DiagnosticsCap);
        // Total Covered — the sum the claim pays out.
        public decimal TotalCovered => DailyAllowanceAmount + ExcessBillCovered + DiagnosticsCovered;

        public string ClaimLimitText =>
            $"Daily allowance: PHP {DailyAllowanceRate:N2}/day (max {MaxAllowanceDays} days) - Excess bill cap: PHP {ExcessBillCap:N0} - Diagnostics: {DiagnosticsCoverageRate:P0} up to PHP {DiagnosticsCap:N0} | Policy Max Limit: PHP {MaxClaimAmount:N2}";

        private Beneficiary? _selectedBeneficiary;
        public Beneficiary? SelectedBeneficiary
        {
            get => _selectedBeneficiary;
            set => SetProperty(ref _selectedBeneficiary, value);
        }
        public bool HasBeneficiaries => Beneficiaries.Count > 0;

        private string _claimType = "Medical";
        private DateTime? _claimDate = DateTime.Today;
        private DateTime? _incidentDate = DateTime.Today;
        private DateTime? _admissionDate;
        private DateTime? _dischargeDate;
        private decimal _amountClaimed;
        private int _admissionDays;
        private decimal _excessBillAmount;
        private decimal _outsideDiagnosticsAmount;
        private string _hospitalClinic = "-- Select Hospital --";
        private string _attendingPhysician = string.Empty;
        private string _incidentDescription = string.Empty;
        private string _remarks = string.Empty;

        public string ClaimType { get => _claimType; set => SetProperty(ref _claimType, value); }
        public DateTime? ClaimDate { get => _claimDate; set => SetProperty(ref _claimDate, value); }
        public DateTime? IncidentDate { get => _incidentDate; set => SetProperty(ref _incidentDate, value); }

        public DateTime? AdmissionDate
        {
            get => _admissionDate;
            set { if (SetProperty(ref _admissionDate, value)) RecomputeAdmissionDays(); }
        }
        public DateTime? DischargeDate
        {
            get => _dischargeDate;
            set { if (SetProperty(ref _dischargeDate, value)) RecomputeAdmissionDays(); }
        }

        public decimal AmountClaimed { get => _amountClaimed; set => SetProperty(ref _amountClaimed, value); }
        public int AdmissionDays
        {
            get => _admissionDays;
            set
            {
                if (SetProperty(ref _admissionDays, value))
                    RefreshDailyAllowance();
            }
        }
        public decimal ExcessBillAmount
        {
            get => _excessBillAmount;
            set { if (SetProperty(ref _excessBillAmount, value)) RefreshDailyAllowance(); }
        }
        public decimal OutsideDiagnosticsAmount
        {
            get => _outsideDiagnosticsAmount;
            set { if (SetProperty(ref _outsideDiagnosticsAmount, value)) RefreshDailyAllowance(); }
        }
        public string HospitalClinic { get => _hospitalClinic; set => SetProperty(ref _hospitalClinic, value); }
        public string AttendingPhysician { get => _attendingPhysician; set => SetProperty(ref _attendingPhysician, value); }
        public string IncidentDescription { get => _incidentDescription; set => SetProperty(ref _incidentDescription, value); }
        public string Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }

        // ── Documents ──────────────────────────────────────────────────
        public ObservableCollection<ClaimDocumentItem> Documents { get; } = new();

        // ── Static Lists ───────────────────────────────────────────────
        public string[] ClaimTypes { get; } =
            { "Medical", "Dental", "Vision", "Life", "Accident",
              "Disability", "Reimbursement", "Other" };
        public string[] DocTypes { get; } =
            { "Medical Report", "Hospital Bill", "Prescription",
              "Death Certificate", "Incident Report", "Lab Result", "Other" };

        // ── State ──────────────────────────────────────────────────────
        private string _errorMessage = string.Empty;
        private bool _isBusy;
        private bool _isLoadingPolicies;

        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
        public bool IsBusy { get => _isBusy; set { SetProperty(ref _isBusy, value); SaveCommand.RaiseCanExecuteChanged(); } }
        public bool IsLoadingPolicies { get => _isLoadingPolicies; set => SetProperty(ref _isLoadingPolicies, value); }
        public string PolicyEmptyMessage =>
            HasEmployee
                ? "No active policy assignment found for this employee."
                : "Select an employee first to see their policies.";
        public string BeneficiaryEmptyMessage =>
            HasEmployee
                ? "No active beneficiaries found for this employee. Leave blank if the employee is the claimant."
                : "Select an employee first to load beneficiaries.";

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand AddDocumentCommand { get; }
        public RelayCommand AddHospitalBillCommand { get; }
        public RelayCommand AddOfficialReceiptCommand { get; }
        public RelayCommand ScanDigitalIdCommand { get; }
        public RelayCommand ScanCameraCommand { get; }
        public RelayCommand<ClaimDocumentItem> RemoveDocumentCommand { get; }

        // ── Digital ID scan (looks up a beneficiary by their printed ID code) ──
        private string _digitalIdCode = string.Empty;
        public string DigitalIdCode { get => _digitalIdCode; set => SetProperty(ref _digitalIdCode, value); }

        // ── Callbacks ──────────────────────────────────────────────────
        public Action? CloseAction { get; set; }
        public Action? OnSaveSuccess { get; set; }

        // ── Constructor ────────────────────────────────────────────────
        public ClaimFormViewModel()
        {
            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsBusy);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
            AddDocumentCommand = new RelayCommand(AddDocument);
            AddHospitalBillCommand = new RelayCommand(() => AddDocumentOfType("Hospital Bill"));
            AddOfficialReceiptCommand = new RelayCommand(() => AddDocumentOfType("Official Receipt"));
            ScanDigitalIdCommand = new RelayCommand(async () => await ScanDigitalIdAsync());
            ScanCameraCommand = new RelayCommand(async () => await ScanCameraAsync());
            RemoveDocumentCommand = new RelayCommand<ClaimDocumentItem>(
                item => { if (item is not null) Documents.Remove(item); });

            Documents.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(HospitalBillFileName));
                OnPropertyChanged(nameof(OfficialReceiptFileName));
            };

            _initialEmployeesLoadTask = LoadEmployeesAsync();
        }

        // ── Init Edit ──────────────────────────────────────────────────
        public async Task InitEditAsync(int claimId)
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var c = await db.Claims
                    .Include(x => x.Employee)
                    .Include(x => x.Policy)
                    .FirstOrDefaultAsync(x => x.ClaimId == claimId);
                if (c is null) return;

                // Only flip into edit mode once the claim actually loaded — otherwise a
                // failed load left an empty form whose Save would overwrite the real
                // claim with defaults.
                IsEditMode = true;
                _editClaimId = claimId;

                await LoadEmployeesAsync();
                SelectedEmployee = _allEmployees.FirstOrDefault(e => e.EmpId == c.EmpId);
                await LoadPoliciesForEmployeeAsync();

                SelectedEmployeePolicy = EmployeePolicies
                    .FirstOrDefault(ep => ep.PolicyId == c.PolicyId);
                ClaimType = c.ClaimType;
                ClaimDate = c.ClaimDate.HasValue
                                          ? c.ClaimDate.Value.ToDateTime(TimeOnly.MinValue) : null;
                IncidentDate = c.IncidentDate.HasValue
                                          ? c.IncidentDate.Value.ToDateTime(TimeOnly.MinValue) : null;
                AdmissionDate = c.AdmissionDate.HasValue
                                          ? c.AdmissionDate.Value.ToDateTime(TimeOnly.MinValue) : null;
                DischargeDate = c.DischargeDate.HasValue
                                          ? c.DischargeDate.Value.ToDateTime(TimeOnly.MinValue) : null;
                AdmissionDays = c.AdmissionDays;
                ExcessBillAmount = c.ExcessBillAmount;
                OutsideDiagnosticsAmount = c.OutsideDiagnosticsAmount;
                AmountClaimed = c.AmountClaimed;
                HospitalClinic = string.IsNullOrWhiteSpace(c.HospitalClinic) ? "-- Select Hospital --" : c.HospitalClinic;
                AttendingPhysician = c.AttendingPhysician ?? string.Empty;
                IncidentDescription = c.IncidentDescription ?? string.Empty;
                Remarks = c.Remarks ?? string.Empty;

                var docs = await db.ClaimDocuments
                    .Where(d => d.ClaimId == claimId).ToListAsync();
                foreach (var d in docs)
                    Documents.Add(new ClaimDocumentItem
                    {
                        DocType = d.DocType,
                        FileName = d.FileName,
                        FilePath = d.FilePath
                    });
            }
            catch (Exception ex)
            {
                // Called fire-and-forget from ClaimFormDialog — without this catch the
                // exception vanished into the unobserved-task handler.
                IsEditMode = false;
                _editClaimId = 0;
                ErrorMessage = $"Could not load the claim for editing: {ex.Message}";
            }
        }

        public async Task InitForBeneficiaryAsync(int beneficiaryId)
        {
            _lockedBeneficiaryId = beneficiaryId;
            OnPropertyChanged(nameof(IsBeneficiaryLocked));
            OnPropertyChanged(nameof(CanChangeClaimant));
            await _initialEmployeesLoadTask;
            await ApplyLockedBeneficiaryAsync();
        }

        // ── Load Employees ─────────────────────────────────────────────
        private async Task LoadEmployeesAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var list = await db.Employees
                    .Include(e => e.Department)
                    .Where(e => e.EmploymentStatus == "Active")
                    .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                    .ToListAsync();
                _allEmployees.Clear();
                foreach (var e in list) _allEmployees.Add(e);
                FilterEmployees();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load employees failed: {ex.Message}";
            }
        }

        private void FilterEmployees()
        {
            FilteredEmployees.Clear();
            var q = _allEmployees.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(EmployeeSearch))
            {
                var s = EmployeeSearch.Trim().ToLower();
                q = q.Where(e => e.FullName.ToLower().Contains(s) ||
                                 e.EmployeeNo.ToLower().Contains(s));
            }
            foreach (var e in q) FilteredEmployees.Add(e);
        }

        // ── Load Policies for Selected Employee ────────────────────────
        private async Task ApplyLockedBeneficiaryAsync()
        {
            if (!_lockedBeneficiaryId.HasValue)
                return;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var beneficiary = await db.Beneficiaries
                    .Include(b => b.Employee)
                    .FirstOrDefaultAsync(b => b.BenId == _lockedBeneficiaryId.Value);
                if (beneficiary is null)
                    return;

                SelectedEmployee = _allEmployees.FirstOrDefault(e => e.EmpId == beneficiary.EmpId) ?? beneficiary.Employee;
                await LoadPoliciesForEmployeeAsync();
                SelectedBeneficiary = Beneficiaries.FirstOrDefault(b => b.BenId == beneficiary.BenId);

                ApplicantName = beneficiary.FullName;
                var snapshot = await HouseholdLookupService.GetHouseholdSnapshotAsync(db, beneficiary);
                IsHouseholdHead = snapshot.IsHouseholdHead;
                FamilyMembers.Clear();
                foreach (var m in snapshot.Members) FamilyMembers.Add(m);
                OnPropertyChanged(nameof(ApplicantName));
                OnPropertyChanged(nameof(IsHouseholdHead));
                OnPropertyChanged(nameof(FamilyRole));
                OnPropertyChanged(nameof(HouseholdStatusBadge));
            }
            catch (Exception ex)
            {
                // Reached fire-and-forget from ClaimVerificationDialog's constructor. If the
                // claimant lock fails to apply, the self-service form must say so instead of
                // opening with no claimant selected.
                ErrorMessage = $"Could not load your beneficiary record: {ex.Message}";
            }
        }

        // Opens the camera QR scanner (same pattern as DistributionBatchViewModel.ScanCameraAsync)
        // and forwards the decoded code into the existing digital-ID lookup path.
        private async Task ScanCameraAsync()
        {
            try
            {
                var dialog = new QRScannerDialog();
                dialog.QRCodeScanned += code =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        DigitalIdCode = code;
                        ScanDigitalIdCommand.Execute(null);
                    });
                };
                await DialogHost.Show(dialog, "ClaimVerificationDialogHost");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Camera scanning is unavailable: {ex.Message}";
            }
        }

        // Resolves a scanned/typed digital-ID code to a beneficiary and selects their
        // employee + beneficiary in the form.
        private async Task ScanDigitalIdAsync()
        {
            var code = DigitalIdCode?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                ErrorMessage = "Scan or enter a digital ID code first.";
                return;
            }

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var beneficiary = await db.Beneficiaries
                    .FirstOrDefaultAsync(b => b.BeneficiaryId == code || b.CivilRegistryId == code);
                if (beneficiary is null)
                {
                    ErrorMessage = $"No beneficiary found for ID \"{code}\".";
                    return;
                }

                // Self-service mode: the scan/code entry only confirms identity against the
                // beneficiary who opened the dialog — it must never switch the claimant.
                if (_lockedBeneficiaryId.HasValue && beneficiary.BenId != _lockedBeneficiaryId.Value)
                {
                    ErrorMessage = "This ID does not match your account — you can only file a claim for yourself.";
                    return;
                }

                ErrorMessage = string.Empty;
                SelectedEmployee = _allEmployees.FirstOrDefault(e => e.EmpId == beneficiary.EmpId);
                await LoadPoliciesForEmployeeAsync();
                SelectedBeneficiary = Beneficiaries.FirstOrDefault(b => b.BenId == beneficiary.BenId);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Digital ID lookup failed: {ex.Message}";
            }
        }

        private async Task LoadPoliciesForEmployeeAsync()
        {
            EmployeePolicies.Clear();
            Beneficiaries.Clear();
            SelectedEmployeePolicy = null;
            SelectedBeneficiary = null;
            OnPropertyChanged(nameof(HasPolicies));
            OnPropertyChanged(nameof(HasBeneficiaries));
            OnPropertyChanged(nameof(PolicyEmptyMessage));
            OnPropertyChanged(nameof(BeneficiaryEmptyMessage));
            if (SelectedEmployee is null) return;

            IsLoadingPolicies = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var policies = await db.EmployeePolicies
                    .Include(ep => ep.Policy)
                    .Where(ep => ep.EmpId == SelectedEmployee.EmpId &&
                                 ep.AssignmentStatus == "Active")
                    .ToListAsync();
                foreach (var p in policies) EmployeePolicies.Add(p);
                if (EmployeePolicies.Count == 1)
                    SelectedEmployeePolicy = EmployeePolicies[0];

                var bens = await db.Beneficiaries
                    .Where(b => b.EmpId == SelectedEmployee.EmpId && b.IsActive)
                    .ToListAsync();
                foreach (var b in bens) Beneficiaries.Add(b);

                await LoadAvailableFundAsync(db);

                OnPropertyChanged(nameof(HasPolicies));
                OnPropertyChanged(nameof(HasBeneficiaries));
                OnPropertyChanged(nameof(PolicyEmptyMessage));
                OnPropertyChanged(nameof(BeneficiaryEmptyMessage));
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load employee policies failed: {ex.Message}";
            }
            finally { IsLoadingPolicies = false; }
        }

        private async Task LoadAvailableFundAsync(eSureHiDbContext db)
        {
            if (SelectedEmployee is null)
            {
                AvailableFundBalance = 0;
                return;
            }

            var groupName = SelectedEmployee.EmploymentType;
            if (string.IsNullOrWhiteSpace(groupName))
            {
                AvailableFundBalance = 0;
                return;
            }

            var fund = await db.SourceFunds
                .FirstOrDefaultAsync(f => f.FundName == groupName);

            if (fund is null)
            {
                AvailableFundBalance = 0;
                return;
            }

            var contributionSum = await db.Beneficiaries
                .Where(b => b.SourceOfFunds == groupName && b.IsActive)
                .SumAsync(b => b.Contribution);

            AvailableFundBalance = fund.AllocatedAmount - (fund.UsedAmount + contributionSum);
        }

        // ── Add Document ───────────────────────────────────────────────
        public const long MaxAttachmentBytes = 5 * 1024 * 1024; // 5 MB

        private void AddDocument() => AddDocumentOfType("Other");

        // Adds an attachment of a specific type (e.g. "Hospital Bill", "Official Receipt").
        // Enforces image/PDF only and the 5 MB size cap.
        private void AddDocumentOfType(string docType)
        {
            var dlg = new OpenFileDialog
            {
                Title = $"Select {docType}",
                Filter = "Image or PDF|*.pdf;*.jpg;*.jpeg;*.png",
                Multiselect = docType == "Other"
            };
            if (dlg.ShowDialog() != true) return;

            foreach (var path in dlg.FileNames)
            {
                var info = new System.IO.FileInfo(path);
                if (info.Length > MaxAttachmentBytes)
                {
                    ErrorMessage = $"\"{info.Name}\" is {info.Length / 1024d / 1024d:N1} MB — attachments must be 5 MB or smaller.";
                    continue;
                }

                // For dedicated slots, replace any existing attachment of the same type.
                if (docType != "Other")
                {
                    var existing = Documents.Where(d => d.DocType == docType).ToList();
                    foreach (var e in existing) Documents.Remove(e);
                }

                Documents.Add(new ClaimDocumentItem
                {
                    DocType = docType,
                    FileName = info.Name,
                    FilePath = path,
                    SizeKb = (int)(info.Length / 1024)
                });
            }
        }

        // ── Validate ───────────────────────────────────────────────────
        private bool Validate()
        {
            if (SelectedEmployee is null)
            { ErrorMessage = "Please select an employee."; return false; }

            // Required text fields
            if (string.IsNullOrWhiteSpace(HospitalClinic) || HospitalClinic == "-- Select Hospital --")
            { ErrorMessage = "Hospital or clinic name is required."; return false; }

            // Numeric input checks
            if (AdmissionDays < 0)
            { ErrorMessage = "Admission days cannot be negative."; return false; }
            if (ExcessBillAmount < 0)
            { ErrorMessage = "Excess bill amount cannot be negative."; return false; }
            if (OutsideDiagnosticsAmount < 0)
            { ErrorMessage = "Outside diagnostics amount cannot be negative."; return false; }

            // Date validation (No future dates)
            if (ClaimDate is null)
            { ErrorMessage = "Claim date is required."; return false; }
            if (ClaimDate.Value.Date > DateTime.Today)
            { ErrorMessage = "Claim date cannot be in the future."; return false; }

            if (IncidentDate.HasValue && IncidentDate.Value.Date > DateTime.Today)
            { ErrorMessage = "Incident date cannot be in the future."; return false; }

            if (AdmissionDate is null || DischargeDate is null)
            { ErrorMessage = "Enter both admission and discharge dates."; return false; }
            
            if (AdmissionDate.Value.Date > DateTime.Today)
            { ErrorMessage = "Admission date cannot be in the future."; return false; }
            if (DischargeDate.Value.Date > DateTime.Today)
            { ErrorMessage = "Discharge date cannot be in the future."; return false; }

            if (DischargeDate.Value.Date < AdmissionDate.Value.Date)
            { ErrorMessage = "Discharge date cannot be earlier than the admission date."; return false; }

            if (TotalCovered <= 0)
            { ErrorMessage = "The claim breakdown totals zero — enter admitted days, excess bill, or diagnostics."; return false; }
            if (Documents.Count == 0)
            { ErrorMessage = "Please attach at least the hospital bill or official receipt."; return false; }
            if (MaxClaimAmount > 0 && TotalCovered > MaxClaimAmount)
            {
                ErrorMessage = $"The claim amount (PHP {TotalCovered:N2}) exceeds the maximum coverage limit for this policy (PHP {MaxClaimAmount:N2}).";
                return false;
            }
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
                // Save the claim and its attachments atomically. The retry-enabled
                // remote context cannot participate in a user-started transaction.
                using var db = eSureHiDbContextFactory.CreateWithoutRetry();
                using var transaction = await db.Database.BeginTransactionAsync();

                // Find or create matching policy on the fly to satisfy DB constraint
                var programName = SelectedEmployee?.EmploymentType;
                if (string.IsNullOrWhiteSpace(programName)) programName = "Job Order";

                // Prefer an Active match — several programs have superseded/Cancelled duplicates.
                var policy = SelectedEmployeePolicy is null
                    ? null
                    : await db.InsurancePolicies.FindAsync(SelectedEmployeePolicy.PolicyId);

                if (policy is null)
                {
                    // policy_type is an enum in the remote IMS database. Employment
                    // labels such as "Contractual" are not valid policy types.
                    var fallbackPolicyType = NormalizePolicyType(programName);
                    policy = await db.InsurancePolicies
                        .Where(p => p.PolicyType == fallbackPolicyType ||
                                    p.PolicyName == programName ||
                                    p.PolicyName == fallbackPolicyType)
                        .OrderByDescending(p => p.PolicyStatus == "Active")
                        .FirstOrDefaultAsync();

                    if (policy is null)
                    {
                        policy = new InsurancePolicy
                        {
                            PolicyCode = $"POL-{fallbackPolicyType.Replace(" ", "").ToUpperInvariant()}",
                            PolicyName = fallbackPolicyType,
                            PolicyType = fallbackPolicyType,
                            CoverageAmount = 1000000m,
                            EffectiveDate = DateOnly.FromDateTime(DateTime.Today),
                            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddYears(1)),
                            PolicyStatus = "Active",
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        };
                        db.InsurancePolicies.Add(policy);
                        await db.SaveChangesAsync();
                    }
                }

                if (IsEditMode)
                {
                    var c = await db.Claims.FindAsync(_editClaimId);
                    if (c is null) { ErrorMessage = "Claim not found."; return; }
                    MapToEntity(c, policy);
                    c.UpdatedAt = DateTime.Now;
                    await db.SaveChangesAsync();
                    await SaveDocumentsAsync(db, c.ClaimId);
                    await transaction.CommitAsync();

                    await AuditService.LogUpdate("claims", c.ClaimId,
                        $"Beneficiary insurance claim updated: {c.ClaimNo} - {c.ClaimType}");

                    await WorkflowService.RecordTransactionAsync(
                        c.ClaimNo,
                        "Insurance Claim",
                        BuildClaimTransactionSubject(c, "Updated"),
                        c.ClaimStatus,
                        c.Remarks);
                }
                else
                {
                    var c = new Claim
                    {
                        ClaimNo = GenerateClaimNo(),
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    MapToEntity(c, policy);
                    c.ClaimStatus = "Submitted";
                    c.SubmittedDate = DateTime.Now;
                    db.Claims.Add(c);
                    await db.SaveChangesAsync();
                    await SaveDocumentsAsync(db, c.ClaimId);
                    await transaction.CommitAsync();

                    await AuditService.LogInsert("claims", c.ClaimId,
                        $"Beneficiary insurance claim submitted: {c.ClaimNo} - {c.ClaimType}");

                    await WorkflowService.RecordTransactionAsync(
                        c.ClaimNo,
                        "Insurance Claim",
                        BuildClaimTransactionSubject(c, "Submitted"),
                        c.ClaimStatus,
                        c.Remarks);
                }

                OnSaveSuccess?.Invoke();
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                // DbUpdateException.Message is always the generic "See the inner exception"
                // text — the actual constraint/column error only lives on the inner chain.
                ErrorMessage = $"Save failed: {GetDetailedErrorMessage(ex)}";
                System.Diagnostics.Debug.WriteLine("Claim save failed: " + ex);
            }
            finally { IsBusy = false; }
        }

        private static string GetDetailedErrorMessage(Exception ex)
        {
            var messages = new List<string>();
            Exception? current = ex;

            while (current is not null)
            {
                if (!string.IsNullOrWhiteSpace(current.Message) &&
                    !messages.Contains(current.Message))
                {
                    messages.Add(current.Message);
                }

                current = current.InnerException;
            }

            return string.Join(" | ", messages);
        }

        private static string NormalizePolicyType(string? value) => value?.Trim().ToUpperInvariant() switch
        {
            "JOB ORDER" => "Job Order",
            "CASUAL" or "CONTRACTUAL" or "PART-TIME" or "PART TIME" or "PROBATIONARY" => "Casual",
            "REGULAR" => "Regular",
            "HEALTH" => "Health",
            "LIFE" => "Life",
            "ACCIDENT" => "Accident",
            "DISABILITY" => "Disability",
            "RETIREMENT" => "Retirement",
            "DENTAL" => "Dental",
            "VISION" => "Vision",
            "SAVINGS" => "Savings",
            _ => "Job Order"
        };

        private static string NormalizeDocumentType(string? value) => value?.Trim().ToUpperInvariant() switch
        {
            "MEDICAL REPORT" => "Medical Report",
            "HOSPITAL BILL" => "Hospital Bill",
            "PRESCRIPTION" => "Prescription",
            "DEATH CERTIFICATE" => "Death Certificate",
            "INCIDENT REPORT" => "Incident Report",
            "LAB RESULT" => "Lab Result",
            // The UI calls this Official Receipt, while the remote IMS enum stores
            // it as a generic supporting attachment.
            "OFFICIAL RECEIPT" or "OTHER" => "Other",
            _ => "Other"
        };

        // fallbackPolicy is the program-matched policy resolved in SaveAsync. claims.policy_id is
        // NOT NULL with an FK to insurance_policies, so it must never be left at 0 — that happens
        // for claimants with no active employee_policies row (e.g. the "Unlinked Beneficiary"
        // placeholder employee) and surfaces only as a generic DbUpdateException.
        private void MapToEntity(Claim c, InsurancePolicy fallbackPolicy)
        {
            c.EmpId = SelectedEmployee!.EmpId;
            if (SelectedEmployeePolicy is not null)
            {
                c.PolicyId = SelectedEmployeePolicy.PolicyId;
            }
            else if (c.PolicyId == 0)
            {
                c.PolicyId = fallbackPolicy.PolicyId;
            }
            c.BenId = SelectedBeneficiary?.BenId;
            c.SourceOfFunds = SelectedEmployee?.EmploymentType;
            c.ClaimType = ClaimType;
            c.ClaimDate = ClaimDate.HasValue
                                       ? DateOnly.FromDateTime(ClaimDate.Value) : null;
            c.IncidentDate = IncidentDate.HasValue
                                       ? DateOnly.FromDateTime(IncidentDate.Value) : null;
            c.AdmissionDate = AdmissionDate.HasValue
                                       ? DateOnly.FromDateTime(AdmissionDate.Value) : null;
            c.DischargeDate = DischargeDate.HasValue
                                       ? DateOnly.FromDateTime(DischargeDate.Value) : null;
            c.AdmissionDays = AdmissionDays;
            c.CoveredAllowanceDays = CoveredAllowanceDays;
            c.DailyAllowanceRate = DailyAllowanceRate;
            c.DailyAllowanceAmount = DailyAllowanceAmount;
            c.ExcessBillAmount = ExcessBillAmount;
            c.OutsideDiagnosticsAmount = OutsideDiagnosticsAmount;
            c.TotalCovered = TotalCovered;
            c.AmountClaimed = AmountClaimed;
            c.HospitalClinic = string.IsNullOrWhiteSpace(HospitalClinic)
                                       ? null : HospitalClinic.Trim();
            c.AttendingPhysician = string.IsNullOrWhiteSpace(AttendingPhysician)
                                       ? null : AttendingPhysician.Trim();
            c.IncidentDescription = string.IsNullOrWhiteSpace(IncidentDescription)
                                       ? null : IncidentDescription.Trim();
            c.Remarks = string.IsNullOrWhiteSpace(Remarks)
                                       ? null : Remarks.Trim();
        }

        private async Task SaveDocumentsAsync(eSureHiDbContext db, int claimId)
        {
            foreach (var doc in Documents)
            {
                bool exists = await db.ClaimDocuments
                    .AnyAsync(d => d.ClaimId == claimId && d.FilePath == doc.FilePath);
                if (exists) continue;

                db.ClaimDocuments.Add(new ClaimDocument
                {
                    ClaimId = claimId,
                    DocType = NormalizeDocumentType(doc.DocType),
                    FileName = doc.FileName,
                    FilePath = doc.FilePath,
                    FileSizeKb = doc.SizeKb,
                    UploadedBy = AuthService.Instance.CurrentUser?.UserId,
                    UploadedAt = DateTime.Now
                });
            }
            await db.SaveChangesAsync();
        }

        private void RefreshDailyAllowance()
        {
            OnPropertyChanged(nameof(CoveredAllowanceDays));
            OnPropertyChanged(nameof(DailyAllowanceRate));
            OnPropertyChanged(nameof(DailyAllowanceAmount));
            OnPropertyChanged(nameof(ExcessBillCovered));
            OnPropertyChanged(nameof(DiagnosticsCovered));
            OnPropertyChanged(nameof(TotalCovered));
            OnPropertyChanged(nameof(ClaimLimitText));
            AmountClaimed = TotalCovered;
        }

        // Auto-computes admitted days from the admission/discharge date pickers.
        private void RecomputeAdmissionDays()
        {
            if (AdmissionDate.HasValue && DischargeDate.HasValue &&
                DischargeDate.Value.Date >= AdmissionDate.Value.Date)
            {
                AdmissionDays = (DischargeDate.Value.Date - AdmissionDate.Value.Date).Days;
            }
        }

        private static string GenerateClaimNo()
            => $"CLM-{DateTime.Now:yyMMddHHmmss}";

        private string BuildClaimTransactionSubject(Claim claim, string action)
        {
            var claimant = SelectedBeneficiary?.FullName ?? SelectedEmployee?.FullName ?? $"Employee ID: {claim.EmpId}";
            return $"{claimant} - {action} {claim.ClaimType} claim - Amount: {claim.AmountClaimed:N2}";
        }
    }

    // ── Supporting class ───────────────────────────────────────────────
    public class ClaimDocumentItem : ObservableObject
    {
        private string _docType = "Other";
        public string DocType { get => _docType; set => SetProperty(ref _docType, value); }
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int SizeKb { get; set; }
    }
}
