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
using eSureHi.Views.Admin.Dialogs;
using eSureHi.Views.Admin.UserControls;

namespace eSureHi.ViewModels.Admin
{
    public class TransactionQueueViewModel : ObservableObject
    {
        private readonly ObservableCollection<DocumentTransaction> _all = new();

        public ObservableCollection<DocumentTransaction> PendingTransactions { get; } = new();
        public ObservableCollection<DocumentTransaction> ActiveTransactions { get; } = new();

        private DocumentTransaction? _selectedTransaction;
        public DocumentTransaction? SelectedTransaction
        {
            get => _selectedTransaction;
            set
            {
                if (SetProperty(ref _selectedTransaction, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                    SelectCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool HasSelection => SelectedTransaction is not null;

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyFilter();
            }
        }

        private string _typeFilter = "All";
        public string TypeFilter
        {
            get => _typeFilter;
            set
            {
                if (SetProperty(ref _typeFilter, value))
                    ApplyFilter();
            }
        }

        private string _priorityFilter = "All";
        public string PriorityFilter
        {
            get => _priorityFilter;
            set
            {
                if (SetProperty(ref _priorityFilter, value))
                    ApplyFilter();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        public int PendingCount => PendingTransactions.Count;
        public int ActiveCount => ActiveTransactions.Count;
        public string QueueCountText => $"{PendingCount + ActiveCount} / {TotalCount}";

        public string[] TypeOptions { get; } =
            { "All", "Incoming", "Outgoing", "Internal Transfer" };

        public string[] PriorityOptions { get; } =
            { "All", "Low", "Normal", "High", "Urgent" };

        public RelayCommand RefreshCommand { get; }
        public RelayCommand SelectCommand { get; }
        public RelayCommand ShowAllCommand { get; }
        public RelayCommand CloseCommand { get; }

        public Action? CloseAction { get; set; }

        public TransactionQueueViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            SelectCommand = new RelayCommand(OpenSelectedTransaction, () => HasSelection);
            ShowAllCommand = new RelayCommand(ShowAllTransactions);
            CloseCommand = new RelayCommand(() => CloseAction?.Invoke());

            PendingTransactions.CollectionChanged += (_, _) => RefreshCounts();
            ActiveTransactions.CollectionChanged += (_, _) => RefreshCounts();

            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var list = await db.DocumentTransactions
                    .Include(t => t.Sender)
                    .Include(t => t.Receiver)
                    .Where(t => t.Status == "Pending" ||
                                t.Status == "In Transit" ||
                                t.Status == "Received")
                    .OrderBy(t => t.Status == "Pending" ? 0 : 1)
                    .ThenByDescending(t => t.Priority == "Urgent")
                    .ThenByDescending(t => t.CreatedAt)
                    .ToListAsync();

                _all.Clear();
                foreach (var transaction in list)
                    _all.Add(transaction);

                TotalCount = _all.Count;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load transaction queue failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilter()
        {
            var query = _all.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.Trim().ToLowerInvariant();
                query = query.Where(t =>
                    t.TransactionNo.ToLowerInvariant().Contains(search) ||
                    t.Subject.ToLowerInvariant().Contains(search) ||
                    (t.Sender?.SenderName.ToLowerInvariant().Contains(search) ?? false) ||
                    (t.Receiver?.ReceiverName.ToLowerInvariant().Contains(search) ?? false));
            }

            if (TypeFilter != "All")
                query = query.Where(t => t.TransactionType == TypeFilter);

            if (PriorityFilter != "All")
                query = query.Where(t => t.Priority == PriorityFilter);

            var filtered = query.ToList();
            PendingTransactions.Clear();
            ActiveTransactions.Clear();

            foreach (var transaction in filtered.Where(t => t.Status == "Pending"))
                PendingTransactions.Add(transaction);

            foreach (var transaction in filtered.Where(t => t.Status != "Pending"))
                ActiveTransactions.Add(transaction);

            if (SelectedTransaction is not null && !filtered.Any(t => t.TransactionId == SelectedTransaction.TransactionId))
                SelectedTransaction = null;

            RefreshCounts();
        }

        private void OpenSelectedTransaction()
        {
            if (SelectedTransaction is null)
                return;

            var dialog = new TransactionDetailDialog(SelectedTransaction.TransactionId);
            dialog.SetStatusChangedCallback(async () => await LoadAsync());
            if (App.ActiveShell is not null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;

            dialog.ShowDialog();
        }

        private void ShowAllTransactions()
        {
            CloseAction?.Invoke();
            NavigationService.Instance.NavigateTo(new TransactionsView());
        }

        private void RefreshCounts()
        {
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(ActiveCount));
            OnPropertyChanged(nameof(QueueCountText));
        }
    }
}
