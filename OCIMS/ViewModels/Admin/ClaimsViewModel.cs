using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;

namespace eSureHi.ViewModels.Admin
{
    public enum ClaimsListMode
    {
        All,
        PendingOnly,
        GroupClaim
    }

    public class ClaimsViewModel : ObservableObject
    {
        private const int MaxClaimantRows = 10;

        // ── Collections ────────────────────────────────────────────────
        private ObservableCollection<Claim> _allClaims = new();
        public ObservableCollection<Claim> DisplayedClaims { get; } = new();

        // ── File-claim beneficiary picker (grouped by program) ─────────
        private readonly ObservableCollection<ClaimBeneficiaryRow> _claimants = new();
        private readonly ListCollectionView _groupedClaimants;
        public ListCollectionView GroupedClaimants => _groupedClaimants;

        private readonly ObservableCollection<string> _claimantPrograms = new();
        public ObservableCollection<string> ClaimantPrograms => _claimantPrograms;

        private string? _selectedClaimantProgram = "All";
        public string? SelectedClaimantProgram
        {
            get => _selectedClaimantProgram;
            set
            {
                // WPF temporarily reports null while an ItemsSource refreshes.
                // Keep the active program instead of treating that transient value
                // as a user selection and reloading the list with no filter.
                if (string.IsNullOrWhiteSpace(value))
                    return;

                if (SetProperty(ref _selectedClaimantProgram, value))
                {
                    _groupedClaimants?.Refresh();
                    // The picker keeps a ten-row cap. Reloading when the source
                    // changes lets the CRS filter use all ten slots for CRS rows
                    // instead of sharing them with the registered-member rows.
                    _ = LoadAsync();
                }
            }
        }

        private bool _isClaimantPickerOpen;
        public bool IsClaimantPickerOpen
        {
            get => _isClaimantPickerOpen;
            set => SetProperty(ref _isClaimantPickerOpen, value);
        }

        private string _claimantSearch = string.Empty;
        public string ClaimantSearch
        {
            get => _claimantSearch;
            set { if (SetProperty(ref _claimantSearch, value)) _groupedClaimants?.Refresh(); }
        }

        // ── Selected ───────────────────────────────────────────────────
        private Claim? _selectedClaim;
        private readonly ClaimsListMode _listMode;
        private static readonly string[] AllStatusOptions =
        {
            "All", "Draft", "Submitted", "Under Review",
            "Approved", "Partially Approved", "Rejected", "Released", "Cancelled"
        };
        private static readonly string[] PendingStatusOptions =
        {
            "All Pending", "Submitted", "Under Review"
        };
        public Claim? SelectedClaim
        {
            get => _selectedClaim;
            set
            {
                SetProperty(ref _selectedClaim, value);
                OnPropertyChanged(nameof(HasSelection));
                ViewCommand.RaiseCanExecuteChanged();
                EditCommand.RaiseCanExecuteChanged();
            }
        }
        public bool HasSelection => SelectedClaim is not null;

        // ── Filters ────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        private string _statusFilter = "All";
        private string _typeFilter = "All";
        private string _groupFilter = "All";
        private string _monthFilter = "All";
        private int? _yearFilter;
        private DateTime? _dateFrom;
        private DateTime? _dateTo;

        public string SearchText
        {
            get => _searchText;
            set { SetProperty(ref _searchText, value); ApplyFilter(); }
        }
        public string StatusFilter
        {
            get => _statusFilter;
            set { SetProperty(ref _statusFilter, value); ApplyFilter(); }
        }
        public string TypeFilter
        {
            get => _typeFilter;
            set { SetProperty(ref _typeFilter, value); ApplyFilter(); }
        }
        public string GroupFilter
        {
            get => _groupFilter;
            set { SetProperty(ref _groupFilter, value); ApplyFilter(); }
        }
        public string MonthFilter
        {
            get => _monthFilter;
            set { SetProperty(ref _monthFilter, value); ApplyFilter(); }
        }
        public int? YearFilter
        {
            get => _yearFilter;
            set { SetProperty(ref _yearFilter, value); ApplyFilter(); }
        }
        public DateTime? DateFrom
        {
            get => _dateFrom;
            set { SetProperty(ref _dateFrom, value); ApplyFilter(); }
        }
        public DateTime? DateTo
        {
            get => _dateTo;
            set { SetProperty(ref _dateTo, value); ApplyFilter(); }
        }

        public string[] StatusOptions =>
            _listMode == ClaimsListMode.PendingOnly
                ? PendingStatusOptions
                : AllStatusOptions;
        public string[] TypeOptions { get; } =
            { "All", "Medical", "Dental", "Vision", "Life",
              "Accident", "Disability", "Reimbursement", "Other" };
        public string[] GroupOptions { get; } =
            { "All", "Job Order", "Casual", "Regular", "Captain" };
        public string[] MonthOptions { get; } =
            { "All", "January", "February", "March", "April", "May", "June",
              "July", "August", "September", "October", "November", "December" };
        public int[] YearOptions { get; } =
            Enumerable.Range(DateTime.Now.Year - 4, 5).Reverse().ToArray();
        public string PageTitle => _listMode switch
        {
            ClaimsListMode.PendingOnly => "Pending Claims",
            ClaimsListMode.GroupClaim => "Group Claim",
            _ => "All Claims"
        };
        public string PageSummaryText => _listMode switch
        {
            ClaimsListMode.PendingOnly =>
                "Claims raised from barangays with insufficient funds, requiring municipal-level approval",
            ClaimsListMode.GroupClaim =>
                "Filter and process claims grouped by funding source/program.",
            _ => "Browse, review, and process all filed claims."
        };
        public bool CanFileClaim => _listMode == ClaimsListMode.All;
        // A logged-in beneficiary may only file for themselves, so the picker — which
        // lists every active beneficiary — stays staff-only.
        public bool CanPickClaimant => CanFileClaim && !AuthService.Instance.IsBeneficiary;
        public bool CanFileOwnClaim => CanFileClaim && AuthService.Instance.IsBeneficiary;
        public bool IsPendingOnly => _listMode == ClaimsListMode.PendingOnly;
        public bool IsAllMode => _listMode == ClaimsListMode.All;
        public bool IsGroupClaimMode => _listMode == ClaimsListMode.GroupClaim;
        public string EmptyStateText => "No claims found matching the selected filters.";

        // ── Counts ─────────────────────────────────────────────────────
        private int _totalCount;
        private int _filteredCount;
        public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
        public int FilteredCount
        {
            get => _filteredCount;
            set { SetProperty(ref _filteredCount, value); OnPropertyChanged(nameof(NoResults)); }
        }
        public bool NoResults => !IsLoading && FilteredCount == 0;

        // ── State ──────────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(NoResults)); }
        }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand NewCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ViewCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand ClearFilterCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }
        public RelayCommand<Claim> PrintVoucherCommand { get; }
        public RelayCommand ToggleClaimantPickerCommand { get; }
        public RelayCommand<ClaimBeneficiaryRow> FileClaimForCommand { get; }
        public RelayCommand<ClaimBeneficiaryRow> ViewClaimCommand { get; }

        // ── Constructor ────────────────────────────────────────────────
        public ClaimsViewModel(ClaimsListMode listMode = ClaimsListMode.All, string? initialSearch = null, string? initialProgram = null)
        {
            _listMode = listMode;
            _statusFilter = _listMode == ClaimsListMode.PendingOnly ? "All Pending" : "All";

            if (!string.IsNullOrEmpty(initialSearch))
            {
                _searchText = initialSearch;
                _claimantSearch = initialSearch;
            }

            if (!string.IsNullOrEmpty(initialProgram) && !string.Equals(initialProgram, "All", StringComparison.OrdinalIgnoreCase))
            {
                _groupFilter = initialProgram;
                _selectedClaimantProgram = initialProgram;
            }

            if (!string.IsNullOrEmpty(initialSearch) || (!string.IsNullOrEmpty(initialProgram) && !string.Equals(initialProgram, "All", StringComparison.OrdinalIgnoreCase)))
            {
                _isClaimantPickerOpen = true;
            }

            NewCommand = new RelayCommand(OpenNewDialog);
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            ViewCommand = new RelayCommand(OpenDetailDialog,
                                     () => SelectedClaim is not null);
            // Submitted claims may still need a missing attachment corrected before
            // review begins.  Claims already under review or beyond remain locked.
            EditCommand = new RelayCommand(OpenEditDialog,
                                     () => SelectedClaim?.ClaimStatus is "Draft" or "Submitted");
            ClearFilterCommand = new RelayCommand(ClearFilters);
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);
            PrintVoucherCommand = new RelayCommand<Claim>(PrintVoucher);
            ToggleClaimantPickerCommand = new RelayCommand(
                () => IsClaimantPickerOpen = !IsClaimantPickerOpen);
            FileClaimForCommand = new RelayCommand<ClaimBeneficiaryRow>(FileClaimFor);
            ViewClaimCommand = new RelayCommand<ClaimBeneficiaryRow>(OpenDetailDialogForClaim);

            _groupedClaimants = (ListCollectionView)CollectionViewSource.GetDefaultView(_claimants);
            _groupedClaimants.Filter = FilterClaimant;

            _ = LoadAsync();
        }

        // ── Load ───────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var claims = await db.Claims
                    .Include(c => c.Employee)
                    .Include(c => c.Policy)
                    .Include(c => c.Beneficiary)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                if (AuthService.Instance.IsBeneficiary)
                {
                    var benId = AuthService.Instance.CurrentUser?.BenId;
                    claims = claims.Where(c => c.BenId == benId).ToList();
                }

                _allClaims.Clear();
                foreach (var c in claims) _allClaims.Add(c);
                TotalCount = _allClaims.Count(MatchesScope);
                ApplyFilter();

                if (CanPickClaimant)
                    await LoadClaimantsAsync(db);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load claims failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsLoading = false; }
        }

        // ── Filter ─────────────────────────────────────────────────────
        private void ApplyFilter()
        {
            var q = _allClaims.Where(MatchesScope);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var s = SearchText.Trim().ToLower();
                q = q.Where(c =>
                    c.ClaimNo.ToLower().Contains(s) ||
                    (c.Employee?.FullName.ToLower().Contains(s) ?? false) ||
                    (c.Beneficiary?.FullName.ToLower().Contains(s) ?? false) ||
                    (c.Policy?.PolicyName.ToLower().Contains(s) ?? false));
            }
            if (_listMode == ClaimsListMode.PendingOnly)
            {
                if (StatusFilter != "All Pending")
                    q = q.Where(c => c.ClaimStatus == StatusFilter);
            }
            else if (StatusFilter != "All")
            {
                q = q.Where(c => c.ClaimStatus == StatusFilter);
            }

            if (TypeFilter != "All") q = q.Where(c => c.ClaimType == TypeFilter);
            if (DateFrom.HasValue)
            {
                var from = DateOnly.FromDateTime(DateFrom.Value);
                q = q.Where(c => c.ClaimDate >= from);
            }
            if (DateTo.HasValue)
            {
                var to = DateOnly.FromDateTime(DateTo.Value);
                q = q.Where(c => c.ClaimDate <= to);
            }

            if (_listMode == ClaimsListMode.GroupClaim)
            {
                if (GroupFilter != "All")
                    q = q.Where(c => c.SourceOfFunds == GroupFilter);
                if (MonthFilter != "All")
                {
                    int monthNum = Array.IndexOf(MonthOptions, MonthFilter);
                    if (monthNum > 0) q = q.Where(c => c.ClaimDate?.Month == monthNum);
                }
                if (YearFilter.HasValue)
                    q = q.Where(c => c.ClaimDate?.Year == YearFilter.Value);
            }

            DisplayedClaims.Clear();
            foreach (var c in q) DisplayedClaims.Add(c);
            FilteredCount = DisplayedClaims.Count;
        }

        // ── New ────────────────────────────────────────────────────────
        private void OpenNewDialog()
        {
            var dialog = AuthService.Instance.IsBeneficiary && AuthService.Instance.CurrentUser?.BenId is int benId
                ? new Views.Admin.Dialogs.ClaimFormDialog(benId, beneficiaryMode: true)
                : new Views.Admin.Dialogs.ClaimFormDialog();
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        // ── Edit ───────────────────────────────────────────────────────
        private void OpenEditDialog()
        {
            if (SelectedClaim is null) return;
            var dialog = new Views.Admin.Dialogs.ClaimFormDialog(SelectedClaim.ClaimId);
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        // ── View Detail ────────────────────────────────────────────────
        private void OpenDetailDialog()
        {
            if (SelectedClaim is null) return;
            var dialog = new Views.Admin.Dialogs.ClaimDetailDialog(SelectedClaim.ClaimId);
            dialog.SetStatusChangedCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        // ── Clear Filters ──────────────────────────────────────────────
        private void ClearFilters()
        {
            _searchText = string.Empty;
            _statusFilter = _listMode == ClaimsListMode.PendingOnly ? "All Pending" : "All";
            _typeFilter = "All";
            _groupFilter = "All";
            _monthFilter = "All";
            _yearFilter = null;
            _dateFrom = null;
            _dateTo = null;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(StatusFilter));
            OnPropertyChanged(nameof(TypeFilter));
            OnPropertyChanged(nameof(GroupFilter));
            OnPropertyChanged(nameof(MonthFilter));
            OnPropertyChanged(nameof(YearFilter));
            OnPropertyChanged(nameof(DateFrom));
            OnPropertyChanged(nameof(DateTo));
            ApplyFilter();
        }

        // ── Print Voucher ──────────────────────────────────────────────
        private void PrintVoucher(Claim? claim)
        {
            if (claim is null) return;
            try
            {
                ReportExportService.PrintClaimVoucher(claim);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print voucher failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Navigation ─────────────────────────────────────────────────
        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }

        // ── Claimant picker ────────────────────────────────────────────
        // Builds the beneficiary list backing the "File Claim" panel.
        private async Task LoadClaimantsAsync(eSureHiDbContext db)
        {
            var beneficiaries = await db.Beneficiaries
                .Include(b => b.Employee)
                .Where(b => b.IsActive)
                .OrderBy(b => b.LastName).ThenBy(b => b.FirstName)
                .ToListAsync();

            var primaryBenIdMap = beneficiaries
                .Where(b => b.IsPrimary)
                .ToDictionary(b => b.EmpId, b => b.BenId);

            var openClaimBenIds = new System.Collections.Generic.HashSet<int>();
            foreach (var c in _allClaims)
            {
                if (!IsOpenClaim(c.ClaimStatus))
                    continue;

                if (c.BenId.HasValue)
                {
                    openClaimBenIds.Add(c.BenId.Value);
                }
                else if (primaryBenIdMap.TryGetValue(c.EmpId, out var primaryBenId))
                {
                    openClaimBenIds.Add(primaryBenId);
                }
            }

            var releasedClaimBenIds = new System.Collections.Generic.HashSet<int>();
            foreach (var c in _allClaims)
            {
                if (c.ClaimStatus != "Released")
                    continue;

                if (c.BenId.HasValue)
                {
                    releasedClaimBenIds.Add(c.BenId.Value);
                }
                else if (primaryBenIdMap.TryGetValue(c.EmpId, out var primaryBenId))
                {
                    releasedClaimBenIds.Add(primaryBenId);
                }
            }

            var releasedDistBenIds = await db.DistributionRecords
                .Where(r => r.Status == "Released")
                .Select(r => r.BeneficiaryId)
                .ToListAsync();
            var releasedDistSet = releasedDistBenIds.ToHashSet();

            // Gather distinct program options from the database (via beneficiaries in-memory)
            var distinctFunds = beneficiaries
                .Where(b => !string.IsNullOrEmpty(b.SourceOfFunds))
                .Select(b => b.SourceOfFunds!.Trim())
                .Distinct()
                .ToList();

            var progList = new System.Collections.Generic.List<string> { "All", "Job Order", "Casual", "Regular", "Captain", "CRS Master List" };
            foreach (var fund in distinctFunds)
            {
                if (!progList.Contains(fund, StringComparer.OrdinalIgnoreCase))
                {
                    progList.Add(fund);
                }
            }

            foreach (var prog in progList)
            {
                if (!_claimantPrograms.Contains(prog, StringComparer.OrdinalIgnoreCase))
                    _claimantPrograms.Add(prog);
            }

            var benefitsList = await db.Benefits
                .Include(x => x.EmployeePolicy)
                .Where(x => x.YearPeriod == DateTime.Today.Year)
                .ToListAsync();

            var benefitByEmpId = benefitsList
                .Where(x => x.EmployeePolicy != null)
                .GroupBy(x => x.EmployeePolicy!.EmpId)
                .ToDictionary(g => g.Key, g => g.First());

            var showCrsOnly = string.Equals(SelectedClaimantProgram, "CRS Master List", StringComparison.OrdinalIgnoreCase);

            _claimants.Clear();
            foreach (var b in showCrsOnly ? Enumerable.Empty<Beneficiary>() : beneficiaries.Take(MaxClaimantRows))
            {
                var claimsForBen = _allClaims
                    .Where(c => c.BenId == b.BenId || (c.BenId == null && c.EmpId == b.EmpId))
                    .ToList();
                var totalClaimed = claimsForBen.Sum(c => c.AmountClaimed);
                var totalApproved = claimsForBen.Sum(c => c.AmountApproved);

                decimal progLimit = 0;
                decimal progRemaining = 0;
                if (benefitByEmpId.TryGetValue(b.EmpId, out var benefit))
                {
                    progLimit = benefit.MaxBenefit;
                    progRemaining = benefit.Remaining;
                }

                var row = new ClaimBeneficiaryRow
                {
                    BenId = b.BenId,
                    BeneficiaryId = FirstNonPlaceholder(
                        b.BeneficiaryId,
                        b.CivilRegistryId,
                        b.Employee?.EmployeeNo,
                        $"BEN-{b.BenId:000000}"),
                    FullName = FirstNonPlaceholder(b.FullName, "Unnamed Member"),
                    Relationship = ManageMembersViewModel.BuildFamilyRole(b),
                    Program = FirstNonPlaceholder(
                        b.SourceOfFunds,
                        b.Employee?.EmploymentType,
                        "Unassigned Program"),
                    HasOpenClaim = openClaimBenIds.Contains(b.BenId),
                    IsReleased = releasedClaimBenIds.Contains(b.BenId),
                    IsReleasedInDistribution = releasedDistSet.Contains(b.BenId),
                    ProgramLimit = progLimit,
                    ProgramRemaining = progRemaining,
                    TotalClaimed = totalClaimed,
                    TotalApproved = totalApproved
                };
                _claimants.Add(row);
            }

            // Add CRS master-list people after the registered insurance members.
            // CRS-only records are useful for discovery, but cannot file a claim
            // until they are registered in the insurance-beneficiary table.
            if (_claimants.Count < MaxClaimantRows)
            {
                var registeredIdentifiers = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var beneficiary in beneficiaries)
                {
                    if (!string.IsNullOrWhiteSpace(beneficiary.BeneficiaryId))
                        registeredIdentifiers.Add(beneficiary.BeneficiaryId.Trim());
                    if (!string.IsNullOrWhiteSpace(beneficiary.CivilRegistryId))
                        registeredIdentifiers.Add(beneficiary.CivilRegistryId.Trim());
                }

                var crsRows = await db.BeneficiaryStaging
                    .AsNoTracking()
                    .Where(row => row.BeneficiaryId != null && row.BeneficiaryId != string.Empty)
                    .OrderBy(row => row.LastName)
                    .ThenBy(row => row.FirstName)
                    .Take(MaxClaimantRows * 3)
                    .ToListAsync();

                foreach (var crs in crsRows)
                {
                    if (_claimants.Count >= MaxClaimantRows)
                        break;

                    var isAlreadyRegistered =
                        (!string.IsNullOrWhiteSpace(crs.BeneficiaryId) && registeredIdentifiers.Contains(crs.BeneficiaryId.Trim())) ||
                        (!string.IsNullOrWhiteSpace(crs.CivilRegistryId) && registeredIdentifiers.Contains(crs.CivilRegistryId.Trim()));
                    if (isAlreadyRegistered)
                        continue;

                    _claimants.Add(new ClaimBeneficiaryRow
                    {
                        BenId = 0,
                        BeneficiaryId = FirstNonPlaceholder(crs.BeneficiaryId, crs.CivilRegistryId),
                        FullName = FirstNonPlaceholder(crs.FullName, $"{crs.LastName}, {crs.FirstName}".Trim(',', ' ')),
                        Relationship = "CRS Member",
                        Program = "CRS Master List",
                        IsCrsOnly = true,
                        ProgramLimit = 0,
                        ProgramRemaining = 0,
                        TotalClaimed = 0,
                        TotalApproved = 0
                    });
                }
            }

            _groupedClaimants.Refresh();
        }

        private static string NormalizeProgramName(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return string.Empty;
            // Replace underscores, hyphens, and multiple spaces with a single space
            var normalized = val.Replace('_', ' ').Replace('-', ' ').Trim();
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");
            return normalized;
        }

        private bool FilterClaimant(object item)
        {
            if (item is not ClaimBeneficiaryRow row) return false;

            // If a program is selected and it is not "All", check for match
            if (!string.IsNullOrEmpty(SelectedClaimantProgram) && !string.Equals(SelectedClaimantProgram, "All", StringComparison.OrdinalIgnoreCase))
            {
                // Program must match SelectedClaimantProgram (case-insensitive and normalized)
                var p1 = NormalizeProgramName(row.Program);
                var p2 = NormalizeProgramName(SelectedClaimantProgram);
                if (!string.Equals(p1, p2, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (string.IsNullOrWhiteSpace(ClaimantSearch)) return true;

            var s = ClaimantSearch.Trim();
            return row.FullName.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                   row.BeneficiaryId.Contains(s, StringComparison.OrdinalIgnoreCase);
        }

        private void FileClaimFor(ClaimBeneficiaryRow? row)
        {
            if (row is null || row.HasOpenClaim) return;

            var dialog = new Views.Admin.Dialogs.ClaimVerificationDialog(row.BenId);
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        private async void OpenDetailDialogForClaim(ClaimBeneficiaryRow? row)
        {
            if (row is null) return;
            using var db = eSureHiDbContextFactory.Create();
            var claim = await db.Claims
                .Where(c => c.BenId == row.BenId)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();
            if (claim != null)
            {
                var dialog = new Views.Admin.Dialogs.ClaimDetailDialog(claim.ClaimId);
                dialog.SetStatusChangedCallback(async () => await LoadAsync());
                if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
                dialog.ShowDialog();
            }
        }

        private static bool IsOpenClaim(string status) =>
            status is "Submitted" or "Under Review" or "Approved" or "Partially Approved";

        private bool MatchesScope(Claim claim) =>
            _listMode != ClaimsListMode.PendingOnly ||
            claim.ClaimStatus is "Submitted" or "Under Review";

        private static string FirstNonPlaceholder(params string?[] values)
        {
            foreach (var val in values)
            {
                if (!string.IsNullOrWhiteSpace(val) &&
                    !string.Equals(val, "None", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(val, "Not set", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(val, "Not specified", StringComparison.OrdinalIgnoreCase))
                {
                    return val.Trim();
                }
            }
            return string.Empty;
        }
    }

    // One row in the program-grouped "File Claim" beneficiary picker.
    public class ClaimBeneficiaryRow
    {
        public int BenId { get; init; }
        public string BeneficiaryId { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Relationship { get; init; } = string.Empty;
        public string Program { get; init; } = string.Empty;
        public bool HasOpenClaim { get; init; }
        public bool IsReleased { get; init; }
        public bool IsReleasedInDistribution { get; init; }
        public bool IsCrsOnly { get; init; }
        public bool CanFile => !IsCrsOnly && (!HasOpenClaim || IsReleasedInDistribution);
        public bool ShowClaimInProgress => HasOpenClaim && !IsReleasedInDistribution;
        public bool ShowViewTransaction => IsReleasedInDistribution;
        public bool ShowRegistrationRequired => IsCrsOnly;

        public decimal ProgramLimit { get; init; }
        public decimal ProgramRemaining { get; init; }
        public decimal TotalClaimed { get; init; }
        public decimal TotalApproved { get; init; }

        public string ProgramInfo => $"₱{ProgramLimit:N2} (Rem: ₱{ProgramRemaining:N2})";
        public string ClaimsInfo => $"₱{TotalClaimed:N2} (Appr: ₱{TotalApproved:N2})";
    }
}
