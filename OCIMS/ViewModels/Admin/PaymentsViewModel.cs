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
    public enum PaymentsListMode
    {
        PendingOnly,
        All
    }

    public class PaymentsViewModel : ObservableObject
    {
        private readonly PaymentsListMode _listMode;
        private ObservableCollection<Payment> _allPayments = new();
        public ObservableCollection<Payment> DisplayedPayments { get; } = new();

        private Payment? _selectedPayment;
        public Payment? SelectedPayment
        {
            get => _selectedPayment;
            set => SetProperty(ref _selectedPayment, value);
        }

        // ── Filters ────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        private string _statusFilter = "All";
        private string _groupFilter = "All";
        private string _monthFilter = "All";
        private int? _yearFilter;
        private DateTime? _dateFrom;
        private DateTime? _dateTo;

        public string SearchText { get => _searchText; set { SetProperty(ref _searchText, value); ApplyFilter(); } }
        public string StatusFilter { get => _statusFilter; set { SetProperty(ref _statusFilter, value); ApplyFilter(); } }
        public string GroupFilter { get => _groupFilter; set { SetProperty(ref _groupFilter, value); ApplyFilter(); } }
        public string MonthFilter { get => _monthFilter; set { SetProperty(ref _monthFilter, value); ApplyFilter(); } }
        public int? YearFilter { get => _yearFilter; set { SetProperty(ref _yearFilter, value); ApplyFilter(); } }
        public DateTime? DateFrom { get => _dateFrom; set { SetProperty(ref _dateFrom, value); ApplyFilter(); } }
        public DateTime? DateTo { get => _dateTo; set { SetProperty(ref _dateTo, value); ApplyFilter(); } }

        public string[] StatusOptions =>
            _listMode == PaymentsListMode.PendingOnly
                ? new[] { "Pending" }
                : new[] { "All", "Pending", "Approved", "Rejected" };
        public string[] GroupOptions { get; } = { "All", "Job Order", "Casual", "Regular", "Captain" };
        public string[] MonthOptions { get; } =
            { "All", "January", "February", "March", "April", "May", "June",
              "July", "August", "September", "October", "November", "December" };
        public int[] YearOptions { get; } = Enumerable.Range(DateTime.Now.Year - 4, 5).Reverse().ToArray();

        public string PageTitle => _listMode == PaymentsListMode.PendingOnly ? "Pending Payment" : "All Payment Ledger";
        public string PageSummaryText => _listMode == PaymentsListMode.PendingOnly
            ? "Review and process payments awaiting approval."
            : "Browse the complete beneficiary payment ledger.";
        public bool IsPendingMode => _listMode == PaymentsListMode.PendingOnly;
        public string EmptyStateText => "No payments found matching the selected filters.";

        private int _totalCount;
        private int _filteredCount;
        public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
        public int FilteredCount
        {
            get => _filteredCount;
            set { SetProperty(ref _filteredCount, value); OnPropertyChanged(nameof(NoResults)); }
        }
        public bool NoResults => !IsLoading && FilteredCount == 0;

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { SetProperty(ref _isLoading, value); OnPropertyChanged(nameof(NoResults)); }
        }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand ClearFilterCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }
        public RelayCommand PrintLedgerCommand { get; }
        public RelayCommand<Payment> ApproveCommand { get; }
        public RelayCommand<Payment> RejectCommand { get; }

        public PaymentsViewModel(PaymentsListMode listMode = PaymentsListMode.All)
        {
            _listMode = listMode;
            _statusFilter = _listMode == PaymentsListMode.PendingOnly ? "Pending" : "All";

            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            ClearFilterCommand = new RelayCommand(ClearFilters);
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);
            PrintLedgerCommand = new RelayCommand(PrintLedger);
            ApproveCommand = new RelayCommand<Payment>(async p => await SetStatusAsync(p, "Approved"),
                p => p?.Status == "Pending" && PermissionService.CanApproveMember);
            RejectCommand = new RelayCommand<Payment>(async p => await SetStatusAsync(p, "Rejected"),
                p => p?.Status == "Pending" && PermissionService.CanReject);

            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var payments = await db.Payments
                    .Include(p => p.Beneficiary)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                _allPayments.Clear();
                foreach (var p in payments) _allPayments.Add(p);
                TotalCount = _allPayments.Count(MatchesScope);
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load payments failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsLoading = false; }
        }

        private bool MatchesScope(Payment p) =>
            _listMode != PaymentsListMode.PendingOnly || p.Status == "Pending";

        private void ApplyFilter()
        {
            var q = _allPayments.Where(MatchesScope);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var s = SearchText.Trim().ToLower();
                q = q.Where(p =>
                    p.MemberName.ToLower().Contains(s) ||
                    (p.FamilyId?.ToLower().Contains(s) ?? false));
            }
            if (_listMode == PaymentsListMode.All && StatusFilter != "All")
                q = q.Where(p => p.Status == StatusFilter);
            if (GroupFilter != "All")
                q = q.Where(p => p.SourceOfFunds == GroupFilter);
            if (MonthFilter != "All")
            {
                int monthNum = Array.IndexOf(MonthOptions, MonthFilter);
                if (monthNum > 0) q = q.Where(p => p.BillingMonth.Month == monthNum);
            }
            if (YearFilter.HasValue)
                q = q.Where(p => p.BillingMonth.Year == YearFilter.Value);
            if (DateFrom.HasValue)
            {
                var from = DateOnly.FromDateTime(DateFrom.Value);
                q = q.Where(p => p.BillingMonth >= from);
            }
            if (DateTo.HasValue)
            {
                var to = DateOnly.FromDateTime(DateTo.Value);
                q = q.Where(p => p.BillingMonth <= to);
            }

            DisplayedPayments.Clear();
            foreach (var p in q) DisplayedPayments.Add(p);
            FilteredCount = DisplayedPayments.Count;
        }

        private async Task SetStatusAsync(Payment? payment, string status)
        {
            if (payment is null) return;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var entity = await db.Payments.FirstOrDefaultAsync(p => p.PaymentId == payment.PaymentId);
                if (entity is null) return;

                entity.Status = status;
                if (status == "Approved") entity.PaidAt = DateOnly.FromDateTime(DateTime.Now);
                await db.SaveChangesAsync();
                await AuditService.LogUpdate("payments", entity.PaymentId,
                    $"Payment for {entity.MemberName} marked {status}");

                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Update payment failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintLedger()
        {
            try
            {
                ReportExportService.PrintPaymentLedger(DisplayedPayments, PageTitle);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Print ledger failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearFilters()
        {
            _searchText = string.Empty;
            _statusFilter = _listMode == PaymentsListMode.PendingOnly ? "Pending" : "All";
            _groupFilter = "All";
            _monthFilter = "All";
            _yearFilter = null;
            _dateFrom = null;
            _dateTo = null;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(StatusFilter));
            OnPropertyChanged(nameof(GroupFilter));
            OnPropertyChanged(nameof(MonthFilter));
            OnPropertyChanged(nameof(YearFilter));
            OnPropertyChanged(nameof(DateFrom));
            OnPropertyChanged(nameof(DateTo));
            ApplyFilter();
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }
    }
}
