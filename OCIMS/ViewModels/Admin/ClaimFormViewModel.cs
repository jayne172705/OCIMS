using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using System;
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

        // ── Step 1: Employee Selection ─────────────────────────────────
        private ObservableCollection<Employee> _allEmployees = new();
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
        public decimal MonthlyContribution => SelectedEmployeePolicy?.EmployeeShare ?? 0;
        public decimal MaxClaimAmount => MonthlyContribution * 100;

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
            SelectedEmployeePolicy is null
                ? "Select a policy to see the claim limit."
                : $"Daily allowance: PHP {DailyAllowanceRate:N2}/day (max {MaxAllowanceDays} days) - Excess bill cap: PHP {ExcessBillCap:N0} - Diagnostics: {DiagnosticsCoverageRate:P0} up to PHP {DiagnosticsCap:N0}";

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
        private string _hospitalClinic = string.Empty;
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
            RemoveDocumentCommand = new RelayCommand<ClaimDocumentItem>(
                item => { if (item is not null) Documents.Remove(item); });

            _ = LoadEmployeesAsync();
        }

        // ── Init Edit ──────────────────────────────────────────────────
        public async Task InitEditAsync(int claimId)
        {
            IsEditMode = true;
            _editClaimId = claimId;

            using var db = eSureHiDbContextFactory.Create();
            var c = await db.Claims
                .Include(x => x.Employee)
                .Include(x => x.Policy)
                .FirstOrDefaultAsync(x => x.ClaimId == claimId);
            if (c is null) return;

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
            HospitalClinic = c.HospitalClinic ?? string.Empty;
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

        public async Task InitForBeneficiaryAsync(int beneficiaryId)
        {
            _lockedBeneficiaryId = beneficiaryId;
            OnPropertyChanged(nameof(IsBeneficiaryLocked));
            OnPropertyChanged(nameof(CanChangeClaimant));
            await LoadEmployeesAsync();
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
                if (_lockedBeneficiaryId.HasValue)
                    await ApplyLockedBeneficiaryAsync();
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

            using var db = eSureHiDbContextFactory.Create();
            var beneficiary = await db.Beneficiaries
                .Include(b => b.Employee)
                .FirstOrDefaultAsync(b => b.BenId == _lockedBeneficiaryId.Value);
            if (beneficiary is null)
                return;

            SelectedEmployee = _allEmployees.FirstOrDefault(e => e.EmpId == beneficiary.EmpId) ?? beneficiary.Employee;
            await LoadPoliciesForEmployeeAsync();
            SelectedBeneficiary = Beneficiaries.FirstOrDefault(b => b.BenId == beneficiary.BenId);
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
            if (SelectedEmployeePolicy is null)
            { ErrorMessage = "Please select a policy."; return false; }
            if (AdmissionDate is null || DischargeDate is null)
            { ErrorMessage = "Enter both admission and discharge dates."; return false; }
            if (DischargeDate.Value.Date < AdmissionDate.Value.Date)
            { ErrorMessage = "Discharge date cannot be earlier than the admission date."; return false; }
            if (TotalCovered <= 0)
            { ErrorMessage = "The claim breakdown totals zero — enter admitted days, excess bill, or diagnostics."; return false; }
            if (ClaimDate is null)
            { ErrorMessage = "Claim date is required."; return false; }
            if (Documents.Count == 0)
            { ErrorMessage = "Please attach at least the hospital bill or official receipt."; return false; }
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

                if (IsEditMode)
                {
                    var c = await db.Claims.FindAsync(_editClaimId);
                    if (c is null) { ErrorMessage = "Claim not found."; return; }
                    MapToEntity(c);
                    c.UpdatedAt = DateTime.Now;
                    await db.SaveChangesAsync();

                    await AuditService.LogUpdate("claims", c.ClaimId,
                        $"Claim edited: {c.ClaimNo} — {c.ClaimType}");

                    await WorkflowService.RecordTransactionAsync(
                        c.ClaimNo,
                        "Insurance Claim",
                        BuildClaimTransactionSubject(c, "Updated"),
                        c.ClaimStatus,
                        c.Remarks);

                    await SaveDocumentsAsync(db, c.ClaimId);
                }
                else
                {
                    var c = new Claim
                    {
                        ClaimNo = GenerateClaimNo(),
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    MapToEntity(c);
                    c.ClaimStatus = "Submitted";
                    c.SubmittedDate = DateTime.Now;
                    db.Claims.Add(c);
                    await db.SaveChangesAsync();

                    await AuditService.LogInsert("claims", c.ClaimId,
                        $"Beneficiary insurance claim submitted: {c.ClaimNo} - {c.ClaimType}");

                    await WorkflowService.RecordTransactionAsync(
                        c.ClaimNo,
                        "Insurance Claim",
                        BuildClaimTransactionSubject(c, "Submitted"),
                        c.ClaimStatus,
                        c.Remarks);

                    await SaveDocumentsAsync(db, c.ClaimId);
                }

                OnSaveSuccess?.Invoke();
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Save failed: {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        private void MapToEntity(Claim c)
        {
            c.EmpId = SelectedEmployee!.EmpId;
            c.PolicyId = SelectedEmployeePolicy!.PolicyId;
            c.BenId = SelectedBeneficiary?.BenId;
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
                    DocType = doc.DocType,
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
