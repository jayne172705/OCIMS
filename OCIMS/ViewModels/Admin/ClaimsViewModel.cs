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
        // ── Collections ────────────────────────────────────────────────
        private ObservableCollection<Claim> _allClaims = new();
        public ObservableCollection<Claim> DisplayedClaims { get; } = new();

        // ── File-claim beneficiary picker (grouped by program) ─────────
        private readonly ObservableCollection<ClaimBeneficiaryRow> _claimants = new();
        private readonly ListCollectionView _groupedClaimants;
        public ListCollectionView GroupedClaimants => _groupedClaimants;

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

        // ── Constructor ────────────────────────────────────────────────
        public ClaimsViewModel(ClaimsListMode listMode = ClaimsListMode.All)
        {
            _listMode = listMode;
            _statusFilter = _listMode == ClaimsListMode.PendingOnly ? "All Pending" : "All";
            NewCommand = new RelayCommand(OpenNewDialog);
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            ViewCommand = new RelayCommand(OpenDetailDialog,
                                     () => SelectedClaim is not null);
            EditCommand = new RelayCommand(OpenEditDialog,
                                     () => SelectedClaim?.ClaimStatus == "Draft");
            ClearFilterCommand = new RelayCommand(ClearFilters);
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);
            PrintVoucherCommand = new RelayCommand<Claim>(PrintVoucher);
            ToggleClaimantPickerCommand = new RelayCommand(
                () => IsClaimantPickerOpen = !IsClaimantPickerOpen);
            FileClaimForCommand = new RelayCommand<ClaimBeneficiaryRow>(FileClaimFor);

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

            var openClaimBenIds = _allClaims
                .Where(c => c.BenId.HasValue && IsOpenClaim(c.ClaimStatus))
                .Select(c => c.BenId!.Value)
                .ToHashSet();

            _claimants.Clear();
            foreach (var b in beneficiaries)
            {
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
                    HasOpenClaim = openClaimBenIds.Contains(b.BenId)
                };
                _claimants.Add(row);
            }

            _groupedClaimants.Refresh();
        }

        private bool FilterClaimant(object item)
        {
            if (item is not ClaimBeneficiaryRow row) return false;

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

        private static bool IsOpenClaim(string status) =>
            status is not ("Rejected" or "Cancelled" or "Released" or "Paid");

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
        public bool CanFile => !HasOpenClaim;
    }
}
