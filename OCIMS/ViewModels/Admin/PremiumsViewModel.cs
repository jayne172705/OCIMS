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
    public class PremiumSearchItem
    {
        public int EmpId { get; set; }
        public string EmployeeNo { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public string? PositionTitle { get; set; }
        public string EmploymentStatus { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PhoneMobile { get; set; }
        public string Address { get; set; } = string.Empty;
        public int PremiumCount { get; set; }
        public int PolicyCount { get; set; }
        public decimal TotalDue { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalBalance { get; set; }
        public string Subtitle =>
            $"{EmployeeNo} - {PremiumCount} premium(s) - Balance {TotalBalance:N2}";
    }

    public class PremiumsViewModel : ObservableObject
    {
        // ── Collections ────────────────────────────────────────────────
        private ObservableCollection<Premium> _allPremiums = new();
        public ObservableCollection<Premium> DisplayedPremiums { get; } = new();
        public ObservableCollection<PremiumSearchItem> SearchResults { get; } = new();

        // ── Selected ───────────────────────────────────────────────────
        private Premium? _selectedPremium;
        public Premium? SelectedPremium
        {
            get => _selectedPremium;
            set
            {
                SetProperty(ref _selectedPremium, value);
                OnPropertyChanged(nameof(HasSelection));
                PayCommand.RaiseCanExecuteChanged();
            }
        }
        public bool HasSelection => SelectedPremium is not null;

        private PremiumSearchItem? _selectedEmployee;
        public PremiumSearchItem? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                SetProperty(ref _selectedEmployee, value);
                OnPropertyChanged(nameof(HasSelectedEmployee));
                OnPropertyChanged(nameof(SelectedEmployeeName));
                OnPropertyChanged(nameof(SelectedEmployeeMeta));
                OnPropertyChanged(nameof(SelectedEmployeeContact));
                OnPropertyChanged(nameof(SelectedEmployeeAddress));
                OnPropertyChanged(nameof(SelectedEmployeeStatus));
                OnPropertyChanged(nameof(SelectedPolicyCount));
            }
        }

        public bool HasSelectedEmployee => SelectedEmployee is not null;
        public string SelectedEmployeeName => SelectedEmployee?.FullName ?? "Premium Details";
        public string SelectedEmployeeMeta => SelectedEmployee is null
            ? string.Empty
            : $"{SelectedEmployee.EmployeeNo} - {SelectedEmployee.Gender ?? "N/A"} - {SelectedEmployee.PositionTitle ?? "No position"}";
        public string SelectedEmployeeContact => SelectedEmployee is null
            ? string.Empty
            : $"{SelectedEmployee.PhoneMobile ?? "No phone"} - {SelectedEmployee.Email ?? "No email"}";
        public string SelectedEmployeeAddress => SelectedEmployee?.Address ?? string.Empty;
        public string SelectedEmployeeStatus => SelectedEmployee?.EmploymentStatus ?? string.Empty;
        public int SelectedPolicyCount => SelectedEmployee?.PolicyCount ?? 0;

        // ── Filters ────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        private string _statusFilter = "All";
        private DateTime? _monthFilter;

        public string SearchText
        {
            get => _searchText;
            set { SetProperty(ref _searchText, value); ApplySearchFilter(); }
        }
        public string StatusFilter
        {
            get => _statusFilter;
            set { SetProperty(ref _statusFilter, value); ApplyFilter(); }
        }
        public DateTime? MonthFilter
        {
            get => _monthFilter;
            set { SetProperty(ref _monthFilter, value); ApplyFilter(); }
        }

        public string[] StatusOptions { get; } =
            { "All", "Unpaid", "Paid", "Partial", "Late", "Waived" };

        // ── Counts + Summary ───────────────────────────────────────────
        private int _totalCount;
        private int _filteredCount;
        private decimal _totalDue;
        private decimal _totalCollected;

        public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
        public int FilteredCount { get => _filteredCount; set => SetProperty(ref _filteredCount, value); }
        public decimal TotalDue { get => _totalDue; set => SetProperty(ref _totalDue, value); }
        public decimal TotalCollected { get => _totalCollected; set => SetProperty(ref _totalCollected, value); }

        // ── State ──────────────────────────────────────────────────────
        private bool _isLoading;
        private bool _isSearchModalOpen;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public bool IsSearchModalOpen { get => _isSearchModalOpen; set => SetProperty(ref _isSearchModalOpen, value); }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand RefreshCommand { get; }
        public RelayCommand GenerateScheduleCommand { get; }
        public RelayCommand PayCommand { get; }
        public RelayCommand ClearFilterCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }
        public RelayCommand SearchAnotherCommand { get; }
        public RelayCommand CloseSearchCommand { get; }
        public RelayCommand<PremiumSearchItem> SelectEmployeeCommand { get; }

        // ── Constructor ────────────────────────────────────────────────
        public PremiumsViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            GenerateScheduleCommand = new RelayCommand(OpenGenerateDialog);
            PayCommand = new RelayCommand(OpenPaymentDialog,
                                          () => SelectedPremium is not null &&
                                                SelectedPremium.PaymentStatus != "Paid" &&
                                                SelectedPremium.PaymentStatus != "Waived");
            ClearFilterCommand = new RelayCommand(ClearFilters);
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);
            SearchAnotherCommand = new RelayCommand(OpenSearchModal);
            CloseSearchCommand = new RelayCommand(() => IsSearchModalOpen = false);
            SelectEmployeeCommand = new RelayCommand<PremiumSearchItem>(SelectEmployee);

            _ = LoadAsync();
        }

        // ── Load ───────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var premiums = await db.Premiums
                    .Include(p => p.EmployeePolicy)
                        .ThenInclude(ep => ep!.Employee)
                    .Include(p => p.EmployeePolicy)
                        .ThenInclude(ep => ep!.Policy)
                    .OrderByDescending(p => p.BillingMonth)
                    .ThenBy(p => p.EmployeePolicy!.Employee!.LastName)
                    .ToListAsync();

                _allPremiums.Clear();
                foreach (var p in premiums) _allPremiums.Add(p);
                TotalCount = _allPremiums.Count;
                ApplySearchFilter();

                IsSearchModalOpen = _allPremiums.Any() && SelectedEmployee is null;

                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Error Loading Premiums",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally { IsLoading = false; }
        }

        // ── Filter ─────────────────────────────────────────────────────
        private void ApplyFilter()
        {
            var q = _allPremiums.AsEnumerable();

            if (SelectedEmployee is not null)
                q = q.Where(p => p.EmployeePolicy?.EmpId == SelectedEmployee.EmpId);
            else
                q = Enumerable.Empty<Premium>();

            if (StatusFilter != "All")
                q = q.Where(p => p.PaymentStatus == StatusFilter);

            if (MonthFilter.HasValue)
            {
                var m = new DateOnly(MonthFilter.Value.Year, MonthFilter.Value.Month, 1);
                q = q.Where(p => p.BillingMonth.Year == m.Year &&
                                 p.BillingMonth.Month == m.Month);
            }

            DisplayedPremiums.Clear();
            foreach (var p in q) DisplayedPremiums.Add(p);

            FilteredCount = DisplayedPremiums.Count;
            TotalDue = DisplayedPremiums.Sum(p => p.TotalAmount + p.LateFee);
            TotalCollected = DisplayedPremiums.Sum(p => p.AmountPaid);
        }

        private void ApplySearchFilter()
        {
            var grouped = _allPremiums
                .Where(p => p.EmployeePolicy?.Employee is not null)
                .GroupBy(p => p.EmployeePolicy!.EmpId)
                .Select(g =>
                {
                    var employee = g.First().EmployeePolicy!.Employee!;
                    return new PremiumSearchItem
                    {
                        EmpId = employee.EmpId,
                        EmployeeNo = employee.EmployeeNo,
                        FullName = employee.FullName,
                        Gender = employee.Gender,
                        PositionTitle = employee.PositionTitle,
                        EmploymentStatus = employee.EmploymentStatus,
                        Email = employee.Email,
                        PhoneMobile = employee.PhoneMobile,
                        Address = string.Join(", ", new[]
                        {
                            employee.AddressLine1,
                            employee.Barangay,
                            employee.City,
                            employee.Province
                        }.Where(part => !string.IsNullOrWhiteSpace(part))),
                        PremiumCount = g.Count(),
                        PolicyCount = g.Select(p => p.EmployeePolicy!.PolicyId).Distinct().Count(),
                        TotalDue = g.Sum(p => p.TotalAmount + p.LateFee),
                        TotalPaid = g.Sum(p => p.AmountPaid),
                        TotalBalance = g.Sum(p => p.Balance)
                    };
                });

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var s = SearchText.Trim().ToLowerInvariant();
                grouped = grouped.Where(x =>
                    x.FullName.ToLowerInvariant().Contains(s) ||
                    x.EmployeeNo.ToLowerInvariant().Contains(s));
            }

            SearchResults.Clear();
            foreach (var item in grouped
                         .OrderBy(x => x.FullName)
                         .Take(50))
            {
                SearchResults.Add(item);
            }
        }

        private void SelectEmployee(PremiumSearchItem? employee)
        {
            if (employee is null) return;

            SelectedEmployee = employee;
            SelectedPremium = null;
            IsSearchModalOpen = false;
            ApplyFilter();
        }

        private void OpenSearchModal()
        {
            if (!_allPremiums.Any())
            {
                IsSearchModalOpen = false;
                return;
            }

            SearchText = string.Empty;
            SelectedPremium = null;
            IsSearchModalOpen = true;
            ApplySearchFilter();
        }

        // ── Generate Schedule ──────────────────────────────────────────
        private void OpenGenerateDialog()
        {
            var dialog = new Views.Admin.Dialogs.GenerateScheduleDialog();
            dialog.SetGeneratedCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();

        }

        // ── Record Payment ─────────────────────────────────────────────
        private void OpenPaymentDialog()
        {
            if (SelectedPremium is null) return;
            var dialog = new Views.Admin.Dialogs.RecordPaymentDialog(
                SelectedPremium.PremiumId);
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        // ── Clear Filters ──────────────────────────────────────────────
        private void ClearFilters()
        {
            _searchText = string.Empty;
            _statusFilter = "All";
            _monthFilter = null;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(StatusFilter));
            OnPropertyChanged(nameof(MonthFilter));
            ApplySearchFilter();
            ApplyFilter();
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }
    }
}
