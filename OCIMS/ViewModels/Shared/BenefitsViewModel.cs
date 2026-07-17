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
    public class BenefitTrackOption
    {
        public string Name { get; init; } = string.Empty;
        public int EmployeeCount { get; init; }
        public int ReleasableCount { get; init; }
        public decimal AllocatedAmount { get; init; }
        public decimal UsedAmount { get; init; }
        public decimal RemainingAmount { get; init; }
        public string Summary => $"{EmployeeCount} employee(s) - {ReleasableCount} ready";
        public string FundSummary => $"Funds: PHP {AllocatedAmount:N2}";
        public string UsedSummary => $"Used: PHP {UsedAmount:N2}";
        public string RemainingSummary => $"Remaining: PHP {RemainingAmount:N2}";
        public bool HasReleasable => ReleasableCount > 0;
        public bool HasFundsRemaining => RemainingAmount > 0;
        public string ReleaseIndicatorText => HasReleasable ? "Ready" : "None";
        public string ReleaseIndicatorHex => HasReleasable ? "#047857" : "#64748B";
        public string ReleaseIndicatorBackgroundHex => HasReleasable ? "#D1FAE5" : "#E2E8F0";
        public string FundIndicatorHex => HasFundsRemaining ? "#047857" : "#B91C1C";
        public string FundIndicatorBackgroundHex => HasFundsRemaining ? "#ECFDF5" : "#FEE2E2";
    }

    public class BenefitDistributionEmployeeItem
    {
        public Employee Employee { get; init; } = new();
        public Benefit? Benefit { get; init; }
        public string EmployeeNo => Employee.EmployeeNo;
        public string FullName => Employee.FullName;
        public string Track => Employee.EmploymentType;
        public string Department => Employee.Department?.DeptName ?? string.Empty;
        public string BenefitType => Benefit?.BenefitType ?? "No benefit assigned";
        public decimal MaxBenefit => Benefit?.MaxBenefit ?? 0;
        public decimal UsedBenefit => Benefit?.UsedBenefit ?? 0;
        public decimal Remaining => Benefit?.Remaining ?? 0;
        public string SourceOfFunds => string.IsNullOrWhiteSpace(Benefit?.SourceOfFunds) ? Track : Benefit.SourceOfFunds!;
        public string Status => Benefit is null
            ? "Setup needed"
            : Benefit.Remaining > 0
                ? "Ready"
                : "Fully released";
        public bool CanRelease => Benefit is not null && Benefit.Remaining > 0;
        public string ReleaseIndicator => CanRelease ? "Ready to release" : Status;
        public string StatusHex => Benefit is null
            ? "#B45309"
            : CanRelease
                ? "#047857"
                : "#475569";
        public string StatusBackgroundHex => Benefit is null
            ? "#FEF3C7"
            : CanRelease
                ? "#D1FAE5"
                : "#E2E8F0";
        public string ActionBackgroundHex => CanRelease ? "#047857" : "#94A3B8";
    }

    public class BenefitsViewModel : ObservableObject
    {
        // ── Collections ────────────────────────────────────────────────
        private ObservableCollection<Benefit> _allBenefits = new();
        private readonly ObservableCollection<BenefitDistributionEmployeeItem> _allDistributionEmployees = new();
        public ObservableCollection<Benefit> DisplayedBenefits { get; } = new();
        public ObservableCollection<BenefitTrackOption> TrackGroups { get; } = new();
        public ObservableCollection<BenefitDistributionEmployeeItem> DistributionEmployees { get; } = new();

        // ── Selected ───────────────────────────────────────────────────
        private Benefit? _selectedBenefit;
        private BenefitDistributionEmployeeItem? _selectedDistributionEmployee;
        private string _selectedTrack = "Job Order";
        public Benefit? SelectedBenefit
        {
            get => _selectedBenefit;
            set
            {
                SetProperty(ref _selectedBenefit, value);
                OnPropertyChanged(nameof(HasSelection));
                EditCommand.RaiseCanExecuteChanged();
                UseCommand.RaiseCanExecuteChanged();
            }
        }
        public bool HasSelection => SelectedBenefit is not null;

        public BenefitDistributionEmployeeItem? SelectedDistributionEmployee
        {
            get => _selectedDistributionEmployee;
            set => SetProperty(ref _selectedDistributionEmployee, value);
        }

        public string SelectedTrack
        {
            get => _selectedTrack;
            set
            {
                if (SetProperty(ref _selectedTrack, value))
                    ApplyDistributionFilter();
            }
        }

        public int DistributionCount => DistributionEmployees.Count;
        public int ReleasableDistributionCount => DistributionEmployees.Count(e => e.CanRelease);

        // ── Filters ────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        private string _typeFilter = "All";
        private int _yearFilter = DateTime.Today.Year;

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
        public int YearFilter
        {
            get => _yearFilter;
            set { SetProperty(ref _yearFilter, value); ApplyFilter(); }
        }

        public string[] TypeOptions { get; } =
            { "All", "Medical", "Dental", "Vision", "Life",
              "Accident", "Optical", "Others" };

        public int[] YearOptions { get; } = Enumerable
            .Range(DateTime.Today.Year - 3, 6)
            .ToArray();

        // ── Counts ─────────────────────────────────────────────────────
        private int _totalCount;
        private int _filteredCount;
        private decimal _totalMax;
        private decimal _totalUsed;
        private decimal _totalRemaining;

        public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
        public int FilteredCount { get => _filteredCount; set => SetProperty(ref _filteredCount, value); }
        public decimal TotalMax { get => _totalMax; set => SetProperty(ref _totalMax, value); }
        public decimal TotalUsed { get => _totalUsed; set => SetProperty(ref _totalUsed, value); }
        public decimal TotalRemaining { get => _totalRemaining; set => SetProperty(ref _totalRemaining, value); }

        // ── State ──────────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand AddCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand UseCommand { get; }
        public RelayCommand<BenefitDistributionEmployeeItem> ReleaseDistributionCommand { get; }
        public RelayCommand ClearFilterCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        // ── Constructor ────────────────────────────────────────────────
        public BenefitsViewModel()
        {
            AddCommand = new RelayCommand(OpenAddDialog);
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            EditCommand = new RelayCommand(OpenEditDialog,
                                     () => SelectedBenefit is not null);
            UseCommand = new RelayCommand(OpenUseDialog,
                                     () => SelectedBenefit is not null &&
                                           SelectedBenefit.Remaining > 0);
            ReleaseDistributionCommand = new RelayCommand<BenefitDistributionEmployeeItem>(OpenDistributionReleaseDialog,
                item => item is not null && item.CanRelease);
            ClearFilterCommand = new RelayCommand(ClearFilters);
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);

            _ = LoadAsync();
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }

        // ── Load ───────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var benefits = await db.Benefits
                    .Include(b => b.EmployeePolicy)
                        .ThenInclude(ep => ep!.Employee)
                    .Include(b => b.EmployeePolicy)
                        .ThenInclude(ep => ep!.Policy)
                    .OrderBy(b => b.EmployeePolicy!.Employee!.LastName)
                    .ThenBy(b => b.YearPeriod)
                    .ToListAsync();

                _allBenefits.Clear();
                foreach (var b in benefits) _allBenefits.Add(b);
                await LoadDistributionAsync(db);
                TotalCount = _allBenefits.Count;
                ApplyFilter();
                ApplyDistributionFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load benefits failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsLoading = false; }
        }

        private async Task LoadDistributionAsync(eSureHiDbContext db)
        {
            var employees = await db.Employees
                .Include(e => e.Department)
                .Where(e => e.EmploymentStatus == "Active" &&
                            (e.EmploymentType == "Job Order" ||
                             e.EmploymentType == "Casual" ||
                             e.EmploymentType == "Regular"))
                .OrderBy(e => e.EmploymentType)
                .ThenBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();

            _allDistributionEmployees.Clear();
            foreach (var employee in employees)
            {
                var benefit = _allBenefits
                    .Where(b => b.EmployeePolicy?.Employee?.EmpId == employee.EmpId &&
                                b.YearPeriod == DateTime.Today.Year)
                    .OrderByDescending(b => b.Remaining)
                    .FirstOrDefault();

                _allDistributionEmployees.Add(new BenefitDistributionEmployeeItem
                {
                    Employee = employee,
                    Benefit = benefit
                });
            }

            var sourceFunds = await db.SourceFunds
                .Where(f => f.Status == "Active" &&
                            (f.FundName == "Job Order" ||
                             f.FundName == "Casual" ||
                             f.FundName == "Regular"))
                .ToListAsync();

            TrackGroups.Clear();
            foreach (var track in new[] { "Job Order", "Casual", "Regular" })
            {
                var rows = _allDistributionEmployees
                    .Where(e => e.Track == track)
                    .ToList();
                var fund = sourceFunds.FirstOrDefault(f => f.FundName == track);

                TrackGroups.Add(new BenefitTrackOption
                {
                    Name = track,
                    EmployeeCount = rows.Count,
                    ReleasableCount = rows.Count(e => e.CanRelease),
                    AllocatedAmount = fund?.AllocatedAmount ?? 0,
                    UsedAmount = fund?.UsedAmount ?? 0,
                    RemainingAmount = fund?.RemainingAmount ?? 0
                });
            }

            if (!TrackGroups.Any(g => g.Name == SelectedTrack))
                SelectedTrack = "Job Order";
        }

        private void ApplyDistributionFilter()
        {
            DistributionEmployees.Clear();
            foreach (var employee in _allDistributionEmployees.Where(e => e.Track == SelectedTrack))
                DistributionEmployees.Add(employee);

            OnPropertyChanged(nameof(DistributionCount));
            OnPropertyChanged(nameof(ReleasableDistributionCount));
        }

        // ── Filter ─────────────────────────────────────────────────────
        private void ApplyFilter()
        {
            var q = _allBenefits.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var s = SearchText.Trim().ToLower();
                q = q.Where(b =>
                    (b.EmployeePolicy?.Employee?.FullName.ToLower().Contains(s) ?? false) ||
                    (b.EmployeePolicy?.Policy?.PolicyName.ToLower().Contains(s) ?? false));
            }
            if (TypeFilter != "All")
                q = q.Where(b => b.BenefitType == TypeFilter);

            q = q.Where(b => b.YearPeriod == YearFilter);

            DisplayedBenefits.Clear();
            foreach (var b in q) DisplayedBenefits.Add(b);

            FilteredCount = DisplayedBenefits.Count;
            TotalMax = DisplayedBenefits.Sum(b => b.MaxBenefit);
            TotalUsed = DisplayedBenefits.Sum(b => b.UsedBenefit);
            TotalRemaining = DisplayedBenefits.Sum(b => b.Remaining);
        }

        // ── Add ────────────────────────────────────────────────────────
        private void OpenAddDialog()
        {
            var dialog = new Views.Admin.Dialogs.BenefitFormDialog();
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        // ── Edit ───────────────────────────────────────────────────────
        private void OpenEditDialog()
        {
            if (SelectedBenefit is null) return;
            var dialog = new Views.Admin.Dialogs.BenefitFormDialog(
                SelectedBenefit.BenefitId);
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        // ── Use Benefit ────────────────────────────────────────────────
        private void OpenUseDialog()
        {
            if (SelectedBenefit is null) return;
            var dialog = new Views.Admin.Dialogs.UseBenefitDialog(
                SelectedBenefit.BenefitId);
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        private void OpenDistributionReleaseDialog(BenefitDistributionEmployeeItem? item)
        {
            if (item?.Benefit is null)
                return;

            SelectedDistributionEmployee = item;
            SelectedBenefit = item.Benefit;
            OpenUseDialog();
        }

        // ── Clear Filters ──────────────────────────────────────────────
        private void ClearFilters()
        {
            _searchText = string.Empty;
            _typeFilter = "All";
            _yearFilter = DateTime.Today.Year;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(TypeFilter));
            OnPropertyChanged(nameof(YearFilter));
            ApplyFilter();
        }
    }
}
