using System;
using System.Collections.Generic;
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
using eSureHi.Views.Admin.Dialogs;
using eSureHi.Views.Admin.UserControls;

namespace eSureHi.ViewModels.Admin
{
    public class ReviewQueueItem : ObservableObject
    {
        public int Id { get; set; }
        public string ReferenceNo { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public object? OriginalSource { get; set; }

        public string DateDisplay => Date.ToString("MMM dd, yyyy");
        
        public string StatusColor => Status switch
        {
            WorkflowStatuses.Pending => "#64748B", // Gray
            "Submitted" => "#3B82F6",             // Blue
            WorkflowStatuses.UnderReview => "#F59E0B", // Amber
            WorkflowStatuses.Approved => "#10B981",    // Emerald
            WorkflowStatuses.Rejected => "#EF4444",    // Red
            WorkflowStatuses.Released => "#8B5CF6",    // Violet
            _ => "#64748B"
        };
    }

    public class ReviewQueueViewModel : ObservableObject
    {
        private ObservableCollection<ReviewQueueItem> _items = new();
        private ListCollectionView _filteredItemsView;
        private string _searchText = string.Empty;
        private string _selectedQueueType = "All Types";
        private string _selectedStatus = "All Statuses";
        private bool _isLoading;

        public ObservableCollection<ReviewQueueItem> Items
        {
            get => _items;
            set
            {
                if (SetProperty(ref _items, value))
                {
                    _filteredItemsView = (ListCollectionView)CollectionViewSource.GetDefaultView(_items);
                    _filteredItemsView.Filter = FilterQueue;
                    _filteredItemsView.GroupDescriptions.Add(new PropertyGroupDescription("Status"));
                    OnPropertyChanged(nameof(FilteredItems));
                }
            }
        }

        private ReviewQueueItem? _selectedItem;
        public ReviewQueueItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                    OpenCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool HasSelection => SelectedItem is not null;
        public ListCollectionView FilteredItems => _filteredItemsView;

        public List<string> QueueTypes { get; } = new()
        {
            "All Types",
            "Beneficiary Validation",
            "Claim Review",
            "Benefit Release",
            "Cedula Verification",
            "Document Transaction",
            "Insurance Beneficiary Approval"
        };

        public List<string> Statuses { get; } = new()
        {
            "All Statuses",
            WorkflowStatuses.Pending,
            WorkflowStatuses.UnderReview,
            WorkflowStatuses.Approved,
            "Partially Approved",
            "In Transit",
            "Received",
            WorkflowStatuses.Rejected,
            WorkflowStatuses.Released
        };

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    _filteredItemsView.Refresh();
            }
        }

        public string SelectedQueueType
        {
            get => _selectedQueueType;
            set
            {
                if (SetProperty(ref _selectedQueueType, value))
                    _filteredItemsView.Refresh();
            }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetProperty(ref _selectedStatus, value))
                    _filteredItemsView.Refresh();
            }
        }

        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand CloseCommand { get; }
        public RelayCommand OpenCommand { get; }
        public RelayCommand ShowAllCommand { get; }

        public Action? CloseAction { get; set; }

        public ReviewQueueViewModel()
        {
            _filteredItemsView = (ListCollectionView)CollectionViewSource.GetDefaultView(_items);
            _filteredItemsView.Filter = FilterQueue;
            _filteredItemsView.GroupDescriptions.Add(new PropertyGroupDescription("Status"));

            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            CloseCommand = new RelayCommand(NavigateToDashboard);
            OpenCommand = new RelayCommand(OpenSelectedItem, () => HasSelection);
            ShowAllCommand = new RelayCommand(() =>
            {
                SelectedQueueType = "All Types";
                SelectedStatus = "All Statuses";
                SearchText = string.Empty;
            });

            _ = LoadAsync();
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new HomeView());
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                var newList = new List<ReviewQueueItem>();

                // 1. Claims
                var claims = await db.Claims
                    .Include(c => c.Employee)
                    .Where(c => c.ClaimStatus != "Draft")
                    .ToListAsync();

                foreach (var c in claims)
                {
                    newList.Add(new ReviewQueueItem
                    {
                        Id = c.ClaimId,
                        ReferenceNo = c.ClaimNo,
                        Name = c.Employee?.FullName ?? "Unknown",
                        Type = c.ClaimStatus == WorkflowStatuses.Approved ? "Benefit Release" : "Claim Review",
                        Status = c.ClaimStatus == "Submitted" ? WorkflowStatuses.Pending : c.ClaimStatus,
                        Date = c.SubmittedDate ?? c.CreatedAt,
                        OriginalSource = c
                    });
                }

                // 2. Document Transactions
                var transactions = await db.DocumentTransactions
                    .Include(t => t.Sender)
                    .Include(t => t.Receiver)
                    .Where(t => t.Status == "Pending" ||
                                t.Status == "In Transit" ||
                                t.Status == "Received")
                    .ToListAsync();

                foreach (var t in transactions)
                {
                    newList.Add(new ReviewQueueItem
                    {
                        Id = t.TransactionId,
                        ReferenceNo = t.TransactionNo,
                        Name = t.Subject,
                        Type = "Document Transaction",
                        Status = t.Status,
                        Date = t.UpdatedAt == default ? t.CreatedAt : t.UpdatedAt,
                        OriginalSource = t
                    });
                }

                // 3. Beneficiaries
                var beneficiaries = await db.Beneficiaries
                    .Select(b => new Beneficiary
                    {
                        BenId = b.BenId,
                        EmpId = b.EmpId,
                        FirstName = b.FirstName,
                        LastName = b.LastName,
                        WorkflowStatus = b.WorkflowStatus,
                        CreatedAt = b.CreatedAt
                    })
                    .ToListAsync();

                foreach (var b in beneficiaries)
                {
                    newList.Add(new ReviewQueueItem
                    {
                        Id = b.BenId,
                        ReferenceNo = $"BEN-{b.BenId}",
                        Name = b.FullName,
                        Type = "Beneficiary Validation",
                        Status = b.WorkflowStatus,
                        Date = b.CreatedAt,
                        OriginalSource = b
                    });
                }

                // 4. Cedulas
                var cedulas = await db.Cedulas
                    .Include(c => c.Employee)
                    .ToListAsync();

                foreach (var c in cedulas)
                {
                    newList.Add(new ReviewQueueItem
                    {
                        Id = c.Id,
                        ReferenceNo = c.CedulaNo,
                        Name = c.Employee?.FullName ?? "Unknown",
                        Type = "Cedula Verification",
                        Status = c.WorkflowStatus,
                        Date = c.CreatedAt,
                        OriginalSource = c
                    });
                }

                // 5. Employee Policies (Insurance Beneficiary Approval?)
                var policies = await db.EmployeePolicies
                    .Include(p => p.Employee)
                    .ToListAsync();

                foreach (var p in policies)
                {
                    newList.Add(new ReviewQueueItem
                    {
                        Id = p.EpId,
                        ReferenceNo = $"POL-{p.EpId}",
                        Name = p.Employee?.FullName ?? "Unknown",
                        Type = "Insurance Beneficiary Approval",
                        Status = p.AssignmentStatus,
                        Date = p.CreatedAt,
                        OriginalSource = p
                    });
                }

                Items = new ObservableCollection<ReviewQueueItem>(newList.OrderByDescending(x => x.Date));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load review queue failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool FilterQueue(object obj)
        {
            if (obj is not ReviewQueueItem item) return false;

            // Search
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLower();
                if (!item.Name.ToLower().Contains(search) && !item.ReferenceNo.ToLower().Contains(search))
                    return false;
            }

            // Queue Type
            if (SelectedQueueType != "All Types" && item.Type != SelectedQueueType)
                return false;

            // Status
            if (SelectedStatus != "All Statuses" && item.Status != SelectedStatus)
                return false;

            return true;
        }

        private void OpenSelectedItem()
        {
            if (SelectedItem is null) return;

            switch (SelectedItem.OriginalSource)
            {
                case Claim claim:
                    var claimDialog = new ClaimDetailDialog(claim.ClaimId);
                    claimDialog.SetStatusChangedCallback(async () => await LoadAsync());
                    if (App.ActiveShell is not null && App.ActiveShell != claimDialog)
                        claimDialog.Owner = App.ActiveShell;
                    claimDialog.ShowDialog();
                    break;

                case DocumentTransaction transaction:
                    var txDialog = new TransactionDetailDialog(transaction.TransactionId);
                    txDialog.SetStatusChangedCallback(async () => await LoadAsync());
                    if (App.ActiveShell is not null && App.ActiveShell != txDialog)
                        txDialog.Owner = App.ActiveShell;
                    txDialog.ShowDialog();
                    break;

                case Beneficiary:
                    CloseAction?.Invoke();
                    NavigationService.Instance.NavigateTo(new BeneficiariesView());
                    break;

                case Cedula:
                    CloseAction?.Invoke();
                    NavigationService.Instance.NavigateTo(new CedulaManagementView());
                    break;

                case EmployeePolicy:
                    CloseAction?.Invoke();
                    NavigationService.Instance.NavigateTo(new PoliciesView());
                    break;
            }
        }
    }
}
