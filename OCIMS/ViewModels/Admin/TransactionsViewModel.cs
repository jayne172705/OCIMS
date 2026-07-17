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
    public class TransactionsViewModel : ObservableObject
    {
        // ── Collections ────────────────────────────────────────────────
        private ObservableCollection<DocumentTransaction> _all = new();
        public ObservableCollection<DocumentTransaction> DisplayedTransactions { get; } = new();

        // ── Selected ───────────────────────────────────────────────────
        private DocumentTransaction? _selected;
        public DocumentTransaction? SelectedTransaction
        {
            get => _selected;
            set
            {
                SetProperty(ref _selected, value);
                OnPropertyChanged(nameof(HasSelection));
                ViewCommand.RaiseCanExecuteChanged();
                EditCommand.RaiseCanExecuteChanged();
            }
        }
        public bool HasSelection => SelectedTransaction is not null;

        // ── Filters ────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        private string _typeFilter = "All";
        private string _statusFilter = "All";
        private string _priorityFilter = "All";

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
        public string PriorityFilter
        {
            get => _priorityFilter;
            set { SetProperty(ref _priorityFilter, value); ApplyFilter(); }
        }

        public string[] TypeOptions { get; } =
            { "All", "Incoming", "Outgoing", "Internal Transfer", "Insurance Claim", "Beneficiary Insurance Claim", "Beneficiary Validation", "Policy Assignment Approval", "Cedula Verification" };
        public string[] StatusOptions { get; } =
            { "All", "Pending", "In Transit", "Received",
              "Acknowledged", "Completed", "Cancelled", "Submitted", "Under Review", "Approved", "Partially Approved", "Rejected", "Released" };
        public string[] PriorityOptions { get; } =
            { "All", "Low", "Normal", "High", "Urgent" };

        // ── Counts ─────────────────────────────────────────────────────
        private int _totalCount;
        private int _filteredCount;
        public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
        public int FilteredCount { get => _filteredCount; set => SetProperty(ref _filteredCount, value); }
        public bool CanUploadToEGoogGov => !AuthService.Instance.IsEmployee;

        // ── State ──────────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand NewCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ViewCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand ClearFilterCommand { get; }
        public RelayCommand UploadToEGoogGovCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        // ── Constructor ────────────────────────────────────────────────
        public TransactionsViewModel()
        {
            NewCommand = new RelayCommand(OpenNewDialog);
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            ViewCommand = new RelayCommand(OpenDetailDialog,
                                     () => SelectedTransaction is not null);
            EditCommand = new RelayCommand(OpenEditDialog,
                                     () => SelectedTransaction?.Status == "Pending");
            ClearFilterCommand = new RelayCommand(ClearFilters);
            UploadToEGoogGovCommand = new RelayCommand(async () => await UploadToEGoogGovAsync(), () => CanUploadToEGoogGov && DisplayedTransactions.Any());
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);

            DisplayedTransactions.CollectionChanged += (s, e) => UploadToEGoogGovCommand.RaiseCanExecuteChanged();

            _ = LoadAsync();
        }

        // ── Load ───────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var list = await db.DocumentTransactions
                    .Include(t => t.Sender)
                    .Include(t => t.Receiver)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                _all.Clear();
                foreach (var t in list) _all.Add(t);
                TotalCount = _all.Count;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load transactions failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsLoading = false; }
        }

        // ── Filter ─────────────────────────────────────────────────────
        private void ApplyFilter()
        {
            var q = _all.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var s = SearchText.Trim().ToLower();
                q = q.Where(t =>
                    t.TransactionNo.ToLower().Contains(s) ||
                    t.Subject.ToLower().Contains(s) ||
                    (t.Sender?.SenderName.ToLower().Contains(s) ?? false) ||
                    (t.Receiver?.ReceiverName.ToLower().Contains(s) ?? false));
            }
            if (TypeFilter != "All") q = q.Where(t => t.TransactionType == TypeFilter);
            if (StatusFilter != "All") q = q.Where(t => t.Status == StatusFilter);
            if (PriorityFilter != "All") q = q.Where(t => t.Priority == PriorityFilter);

            DisplayedTransactions.Clear();
            foreach (var t in q) DisplayedTransactions.Add(t);
            FilteredCount = DisplayedTransactions.Count;
        }

        // ── New ────────────────────────────────────────────────────────
        private void OpenNewDialog()
        {
            var dialog = new Views.Admin.Dialogs.TransactionFormDialog();
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        // ── Edit ───────────────────────────────────────────────────────
        private void OpenEditDialog()
        {
            if (SelectedTransaction is null) return;
            var dialog = new Views.Admin.Dialogs.TransactionFormDialog(
                SelectedTransaction.TransactionId);
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        // ── View Detail ────────────────────────────────────────────────
        private void OpenDetailDialog()
        {
            if (SelectedTransaction is null) return;
            var dialog = new Views.Admin.Dialogs.TransactionDetailDialog(
                SelectedTransaction.TransactionId);
            dialog.SetStatusChangedCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        // ── Clear Filters ──────────────────────────────────────────────
        private void ClearFilters()
        {
            _searchText = string.Empty;
            _typeFilter = "All";
            _statusFilter = "All";
            _priorityFilter = "All";
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(TypeFilter));
            OnPropertyChanged(nameof(StatusFilter));
            OnPropertyChanged(nameof(PriorityFilter));
            ApplyFilter();
        }

        // ── eGoogGOV ───────────────────────────────────────────────────
        private async Task UploadToEGoogGovAsync()
        {
            if (!DisplayedTransactions.Any()) return;
            
            IsLoading = true;
            try
            {
                var success = await eSureHi.Services.eGoogGovService.ExportConsolidatedTransactionAsync(DisplayedTransactions);
                if (success)
                {
                    MessageBox.Show("Distributed Transactions successfully consolidated and uploaded to eGoogGOV Portal.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }
    }
}
