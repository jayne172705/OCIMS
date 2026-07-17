using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
        PendingOnly
    }

    public class ClaimsViewModel : ObservableObject
    {
        // ── Collections ────────────────────────────────────────────────
        private ObservableCollection<Claim> _allClaims = new();
        public ObservableCollection<Claim> DisplayedClaims { get; } = new();

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
        public string PageTitle =>
            _listMode == ClaimsListMode.PendingOnly ? "Pending Claims" : "All Claims";
        public string PageSummaryText =>
            _listMode == ClaimsListMode.PendingOnly
                ? "Claims raised from barangays with insufficient funds, requiring municipal-level approval"
                : "Browse, review, and process all filed claims.";
        public bool CanFileClaim => _listMode != ClaimsListMode.PendingOnly;
        public bool IsPendingOnly => _listMode == ClaimsListMode.PendingOnly;
        public bool IsAllMode => _listMode == ClaimsListMode.All;

        // ── Counts ─────────────────────────────────────────────────────
        private int _totalCount;
        private int _filteredCount;
        public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
        public int FilteredCount { get => _filteredCount; set => SetProperty(ref _filteredCount, value); }

        // ── State ──────────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand NewCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ViewCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand ClearFilterCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

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
            _dateFrom = null;
            _dateTo = null;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(StatusFilter));
            OnPropertyChanged(nameof(TypeFilter));
            OnPropertyChanged(nameof(DateFrom));
            OnPropertyChanged(nameof(DateTo));
            ApplyFilter();
        }

        // ── Navigation ─────────────────────────────────────────────────
        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }

        private bool MatchesScope(Claim claim) =>
            _listMode != ClaimsListMode.PendingOnly ||
            claim.ClaimStatus is "Submitted" or "Under Review";
    }
}
