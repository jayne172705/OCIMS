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
using eSureHi.Views.Admin.UserControls;

namespace eSureHi.ViewModels.Admin
{
    public class BeneficiaryQueueItem : ObservableObject
    {
        public long Id { get; set; }
        public string? StagingId { get; set; }
        public string BeneficiaryNo { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Barangay { get; set; } = string.Empty;
        public string InsuranceType { get; set; } = string.Empty;
        public string SourceOfFunds { get; set; } = string.Empty;
        public string QualificationStatus { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public object? OriginalSource { get; set; }

        public string DateDisplay => Date.ToString("MMM dd, yyyy");

        public string StatusDisplay => CurrentStatus switch
        {
            "Pending Verification" => "Pending Verification",
            WorkflowStatuses.UnderReview => "Under Review",
            WorkflowStatuses.Verified => "Verified",
            WorkflowStatuses.Approved => "Approved for Insurance",
            WorkflowStatuses.Released => "Released Benefits",
            WorkflowStatuses.Rejected => "Rejected",
            _ => CurrentStatus
        };

        public string StatusColor => CurrentStatus switch
        {
            "Pending Verification" => "#64748B",
            WorkflowStatuses.UnderReview => "#F59E0B",
            WorkflowStatuses.Verified => "#3B82F6",
            WorkflowStatuses.Approved => "#10B981",
            WorkflowStatuses.Released => "#8B5CF6",
            WorkflowStatuses.Rejected => "#EF4444",
            _ => "#64748B"
        };
    }

    public class BeneficiaryQueueViewModel : ObservableObject
    {
        private ObservableCollection<BeneficiaryQueueItem> _items = new();
        private ListCollectionView _filteredItemsView;
        private string _searchText = string.Empty;
        private string _selectedStatus = "All Statuses";
        private bool _isLoading;

        public ObservableCollection<BeneficiaryQueueItem> Items
        {
            get => _items;
            set
            {
                if (SetProperty(ref _items, value))
                {
                    _filteredItemsView = (ListCollectionView)CollectionViewSource.GetDefaultView(_items);
                    _filteredItemsView.Filter = FilterQueue;
                    _filteredItemsView.GroupDescriptions.Clear();
                    _filteredItemsView.GroupDescriptions.Add(new PropertyGroupDescription("StatusDisplay"));
                    OnPropertyChanged(nameof(FilteredItems));
                }
            }
        }

        private BeneficiaryQueueItem? _selectedItem;
        public BeneficiaryQueueItem? SelectedItem
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

        public List<string> Statuses { get; } = new()
        {
            "All Statuses",
            "Pending Verification",
            "Under Review",
            "Verified",
            "Approved for Insurance",
            "Released Benefits",
            "Rejected"
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
        public Action<BeneficiaryQueueItem>? OnItemSelected { get; set; }

        public BeneficiaryQueueViewModel()
        {
            _filteredItemsView = (ListCollectionView)CollectionViewSource.GetDefaultView(_items);
            _filteredItemsView.Filter = FilterQueue;
            _filteredItemsView.GroupDescriptions.Add(new PropertyGroupDescription("StatusDisplay"));

            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            CloseCommand = new RelayCommand(() => CloseAction?.Invoke());
            OpenCommand = new RelayCommand(OpenSelectedItem, () => HasSelection);
            ShowAllCommand = new RelayCommand(() =>
            {
                SelectedStatus = "All Statuses";
                SearchText = string.Empty;
            });

            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                var newList = new List<BeneficiaryQueueItem>();

                // 1. Pending Verification (CRS Staging - Unlinked)
                var staging = await db.BeneficiaryStaging
                    .Where(b => b.LinkStatus == "Unlinked")
                    .OrderByDescending(b => b.StagingId)
                    .Take(500) // Limit to avoid performance issues
                    .ToListAsync();

                foreach (var s in staging)
                {
                    newList.Add(new BeneficiaryQueueItem
                    {
                        Id = s.StagingId,
                        BeneficiaryNo = s.BeneficiaryId ?? $"CRS-{s.StagingId}",
                        FullName = s.FullName ?? $"{s.FirstName} {s.LastName}",
                        Barangay = s.Address ?? "Unknown", // Assuming Address contains Barangay info or is used for it
                        InsuranceType = "CRS Import",
                        SourceOfFunds = "CRS Master List",
                        QualificationStatus = "Pending Review",
                        CurrentStatus = "Pending Verification",
                        Date = DateTime.Now, // Staging might not have a proper date
                        OriginalSource = s
                    });
                }

                // 2. System Beneficiaries
                var beneficiaries = await db.Beneficiaries
                    .Include(b => b.Employee)
                    .OrderByDescending(b => b.CreatedAt)
                    .ToListAsync();

                foreach (var b in beneficiaries)
                {
                    newList.Add(new BeneficiaryQueueItem
                    {
                        Id = b.BenId,
                        BeneficiaryNo = b.BeneficiaryId ?? $"BEN-{b.BenId}",
                        FullName = b.FullName,
                        Barangay = b.Employee?.Barangay ?? "Unknown",
                        InsuranceType = b.RecipientsInsurance ?? "Standard Insurance",
                        SourceOfFunds = b.SourceOfFunds ?? "LGU Sulop",
                        QualificationStatus = b.WorkflowStatus == WorkflowStatuses.Approved ? "Qualified" : "Reviewing",
                        CurrentStatus = b.WorkflowStatus,
                        Date = b.CreatedAt,
                        OriginalSource = b
                    });
                }

                Items = new ObservableCollection<BeneficiaryQueueItem>(newList.OrderByDescending(x => x.Date));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load beneficiary queue failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool FilterQueue(object obj)
        {
            if (obj is not BeneficiaryQueueItem item) return false;

            // Search
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLower();
                if (!item.FullName.ToLower().Contains(search) && !item.BeneficiaryNo.ToLower().Contains(search))
                    return false;
            }

            // Status
            if (SelectedStatus != "All Statuses" && item.StatusDisplay != SelectedStatus)
                return false;

            return true;
        }

        private void OpenSelectedItem()
        {
            if (SelectedItem is null) return;
            OnItemSelected?.Invoke(SelectedItem);
            CloseAction?.Invoke();
        }
    }
}
