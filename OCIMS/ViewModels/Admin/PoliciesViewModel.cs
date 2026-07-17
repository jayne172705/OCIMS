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
    public class PoliciesViewModel : ObservableObject
    {
        // ── Collections ────────────────────────────────────────────────
        private ObservableCollection<InsurancePolicy> _allPolicies = new();
        public ObservableCollection<InsurancePolicy> DisplayedPolicies { get; } = new();

        // ── Selected ───────────────────────────────────────────────────
        private InsurancePolicy? _selectedPolicy;
        public InsurancePolicy? SelectedPolicy
        {
            get => _selectedPolicy;
            set
            {
                SetProperty(ref _selectedPolicy, value);
                OnPropertyChanged(nameof(HasSelection));
                EditCommand.RaiseCanExecuteChanged();
                DeactivateCommand.RaiseCanExecuteChanged();
            }
        }
        public bool HasSelection => SelectedPolicy is not null;

        // ── Search + Filter ────────────────────────────────────────────
        private string _searchText = string.Empty;
        private string _typeFilter = "All";
        private string _statusFilter = "All";

        public string SearchText
        {
            get => _searchText;
            set { SetProperty(ref _searchText, value); ApplyFilter(); }
        }
        public string TypeFilter
        {
            get => _typeFilter;
            set { SetProperty(ref _typeFilter, value); ApplyFilter(); }
        }
        public string StatusFilter
        {
            get => _statusFilter;
            set { SetProperty(ref _statusFilter, value); ApplyFilter(); }
        }

        public string[] TypeOptions { get; } =
            { "All", "Job Order", "Casual", "Regular" };
        public string[] StatusOptions { get; } =
            { "All", "Active", "Expired", "Lapsed", "Cancelled", "Renewal" };

        // ── Counts ─────────────────────────────────────────────────────
        private int _totalCount;
        private int _filteredCount;
        public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
        public int FilteredCount { get => _filteredCount; set => SetProperty(ref _filteredCount, value); }

        // ── State ──────────────────────────────────────────────────────
        private bool _isLoading;
        private string _statusMessage = string.Empty;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand RefreshCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeactivateCommand { get; }
        public RelayCommand ClearFilterCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        // ── Constructor ────────────────────────────────────────────────
        public PoliciesViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            EditCommand = new RelayCommand(OpenEditDialog,
                                 () => SelectedPolicy is not null);
            DeactivateCommand = new RelayCommand(async () => await DeactivateAsync(),
                                 () => SelectedPolicy is not null &&
                                       SelectedPolicy.PolicyStatus == "Active");
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
                await EnsureEmployeeTrackPoliciesAsync(db);

                var trackCodes = EmployeeTrackDefinitions.Select(t => t.Code).ToArray();
                var policies = await db.InsurancePolicies
                    .Where(p => trackCodes.Contains(p.PolicyCode))
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                _allPolicies.Clear();
                foreach (var p in policies)
                    _allPolicies.Add(p);

                TotalCount = _allPolicies.Count;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static readonly (string Code, string Name, string Type, decimal Budget)[] EmployeeTrackDefinitions =
        {
            ("POL-JOBORDER-001", "Job Order", "Job Order", 250000m),
            ("POL-CASUAL-001", "Casual", "Casual", 500000m),
            ("POL-REGULAR-001", "Regular", "Regular", 1000000m)
        };

        private static async Task EnsureEmployeeTrackPoliciesAsync(eSureHiDbContext db)
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(DateTime.Today);
            var nextYear = today.AddYears(1);
            var reviewDate = nextYear.AddMonths(-1);

            foreach (var track in EmployeeTrackDefinitions)
            {
                var policy = await db.InsurancePolicies
                    .FirstOrDefaultAsync(p => p.PolicyCode == track.Code);

                if (policy is null)
                {
                    policy = new InsurancePolicy
                    {
                        PolicyCode = track.Code,
                        CreatedBy = AuthService.Instance.CurrentUser?.UserId,
                        CreatedAt = now
                    };
                    db.InsurancePolicies.Add(policy);
                }

                policy.PolicyName = track.Name;
                policy.PolicyType = track.Type;
                policy.ProviderName = "LGU Sulop HRMO / Budget Office";
                policy.ProviderContact = "(082) 123-4567";
                policy.EffectiveDate = today;
                policy.ExpiryDate = nextYear;
                policy.RenewalDate = reviewDate;
                policy.CoverageAmount = track.Budget;
                policy.Description = $"Budget track for {track.Name} employee applications and assignments.";
                policy.TermsConditions = "When this budget is depleted, request additional funding through GGMS.";
                policy.PolicyStatus = "Active";
                policy.UpdatedAt = now;
            }

            await db.SaveChangesAsync();
        }

        // ── Filter ─────────────────────────────────────────────────────
        private void ApplyFilter()
        {
            var q = _allPolicies.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var s = SearchText.Trim().ToLower();
                q = q.Where(p =>
                    p.PolicyName.ToLower().Contains(s) ||
                    p.PolicyCode.ToLower().Contains(s) ||
                    (p.ProviderName?.ToLower().Contains(s) ?? false));
            }
            if (TypeFilter != "All") q = q.Where(p => p.PolicyType == TypeFilter);
            if (StatusFilter != "All") q = q.Where(p => p.PolicyStatus == StatusFilter);

            DisplayedPolicies.Clear();
            foreach (var p in q)
                DisplayedPolicies.Add(p);

            FilteredCount = DisplayedPolicies.Count;
        }

        // ── Add ────────────────────────────────────────────────────────
        // ── Edit ───────────────────────────────────────────────────────
        private void OpenEditDialog()
        {
            if (SelectedPolicy is null) return;
            var dialog = new Views.Admin.Dialogs.PolicyFormDialog(SelectedPolicy.PolicyId);
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        // ── Assign ─────────────────────────────────────────────────────
        // ── Deactivate ─────────────────────────────────────────────────
        private async Task DeactivateAsync()
        {
            if (SelectedPolicy is null) return;

            var result = MessageBox.Show(
                $"Cancel policy '{SelectedPolicy.PolicyName}'?\n\n" +
                "This will set the policy status to Cancelled. " +
                "Existing employee assignments will not be affected.",
                "Confirm Cancel Policy",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var p = await db.InsurancePolicies.FindAsync(SelectedPolicy.PolicyId);
                if (p is null) return;
                p.PolicyStatus = "Cancelled";
                p.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Clear Filters ──────────────────────────────────────────────
        private void ClearFilters()
        {
            _searchText = string.Empty;
            _typeFilter = "All";
            _statusFilter = "All";
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(TypeFilter));
            OnPropertyChanged(nameof(StatusFilter));
            ApplyFilter();
        }

        // ── Navigation ─────────────────────────────────────────────────
        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }
    }
}
