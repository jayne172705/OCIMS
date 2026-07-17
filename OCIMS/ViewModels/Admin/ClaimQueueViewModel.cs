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
    public class ClaimQueueViewModel : ObservableObject
    {
        private readonly ObservableCollection<Claim> _all = new();
        private readonly bool _openSelectionInReviewPage;

        public ObservableCollection<Claim> SubmittedClaims { get; } = new();
        public ObservableCollection<Claim> UnderReviewClaims { get; } = new();
        public ObservableCollection<Claim> ApprovedClaims { get; } = new();
        public ObservableCollection<Claim> ClosedClaims { get; } = new();

        private Claim? _selectedClaim;
        public Claim? SelectedClaim
        {
            get => _selectedClaim;
            set
            {
                if (SetProperty(ref _selectedClaim, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                    SelectCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool HasSelection => SelectedClaim is not null;
        public string DialogTitle => _openSelectionInReviewPage
            ? "Review Queue - Select Application"
            : "Review Queue - Select Record";
        public string SelectionTitle => _openSelectionInReviewPage
            ? "Select Application"
            : "Select Record";

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

        public int SubmittedCount => SubmittedClaims.Count;
        public int UnderReviewCount => UnderReviewClaims.Count;
        public int ApprovedCount => ApprovedClaims.Count;
        public int ClosedCount => ClosedClaims.Count;
        public string QueueCountText => $"{SubmittedCount + UnderReviewCount + ApprovedCount} / {TotalCount}";

        public string[] TypeOptions { get; } =
        {
            "All", "Medical", "Dental", "Vision", "Life",
            "Accident", "Disability", "Reimbursement", "Other"
        };

        public RelayCommand RefreshCommand { get; }
        public RelayCommand SelectCommand { get; }
        public RelayCommand ShowAllCommand { get; }
        public RelayCommand CloseCommand { get; }

        public Action? CloseAction { get; set; }

        public ClaimQueueViewModel(bool openSelectionInReviewPage = false)
        {
            _openSelectionInReviewPage = openSelectionInReviewPage;
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            SelectCommand = new RelayCommand(OpenSelectedClaim, () => HasSelection);
            ShowAllCommand = new RelayCommand(ShowAllClaims);
            CloseCommand = new RelayCommand(() => CloseAction?.Invoke());

            SubmittedClaims.CollectionChanged += (_, _) => RefreshCounts();
            UnderReviewClaims.CollectionChanged += (_, _) => RefreshCounts();
            ApprovedClaims.CollectionChanged += (_, _) => RefreshCounts();
            ClosedClaims.CollectionChanged += (_, _) => RefreshCounts();

            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var claims = await db.Claims
                    .Include(c => c.Employee)
                    .Include(c => c.Policy)
                    .Where(c => c.ClaimStatus == "Submitted" ||
                                c.ClaimStatus == "Under Review" ||
                                c.ClaimStatus == "Approved" ||
                                c.ClaimStatus == "Partially Approved" ||
                                c.ClaimStatus == "Released" ||
                                c.ClaimStatus == "Rejected")
                    .OrderBy(c => c.ClaimStatus == "Submitted" ? 0 :
                                  c.ClaimStatus == "Under Review" ? 1 :
                                  c.ClaimStatus == "Approved" || c.ClaimStatus == "Partially Approved" ? 2 : 3)
                    .ThenByDescending(c => c.UpdatedAt)
                    .ToListAsync();

                _all.Clear();
                foreach (var claim in claims)
                    _all.Add(claim);

                TotalCount = _all.Count;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load claims queue failed: {ex.Message}", "Error",
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
                query = query.Where(c =>
                    c.ClaimNo.ToLowerInvariant().Contains(search) ||
                    c.ClaimType.ToLowerInvariant().Contains(search) ||
                    (c.Employee?.FullName.ToLowerInvariant().Contains(search) ?? false) ||
                    (c.Policy?.PolicyName.ToLowerInvariant().Contains(search) ?? false));
            }

            if (TypeFilter != "All")
                query = query.Where(c => c.ClaimType == TypeFilter);

            var filtered = query.ToList();
            SubmittedClaims.Clear();
            UnderReviewClaims.Clear();
            ApprovedClaims.Clear();
            ClosedClaims.Clear();

            foreach (var claim in filtered.Where(c => c.ClaimStatus == "Submitted"))
                SubmittedClaims.Add(claim);

            foreach (var claim in filtered.Where(c => c.ClaimStatus == "Under Review"))
                UnderReviewClaims.Add(claim);

            foreach (var claim in filtered.Where(c => c.ClaimStatus is "Approved" or "Partially Approved"))
                ApprovedClaims.Add(claim);

            foreach (var claim in filtered.Where(c => c.ClaimStatus is "Released" or "Rejected"))
                ClosedClaims.Add(claim);

            if (SelectedClaim is not null && !filtered.Any(c => c.ClaimId == SelectedClaim.ClaimId))
                SelectedClaim = null;

            RefreshCounts();
        }

        private void OpenSelectedClaim()
        {
            if (SelectedClaim is null)
                return;

            if (_openSelectionInReviewPage)
            {
                var claimId = SelectedClaim.ClaimId;
                CloseAction?.Invoke();
                NavigationService.Instance.NavigateTo(new ReviewQueueView(claimId));
                return;
            }

            var dialog = new ClaimDetailDialog(SelectedClaim.ClaimId);
            dialog.SetStatusChangedCallback(async () => await LoadAsync());
            if (App.ActiveShell is not null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;

            dialog.ShowDialog();
        }

        private void ShowAllClaims()
        {
            CloseAction?.Invoke();
            NavigationService.Instance.NavigateTo(new ClaimsView());
        }

        private void RefreshCounts()
        {
            OnPropertyChanged(nameof(SubmittedCount));
            OnPropertyChanged(nameof(UnderReviewCount));
            OnPropertyChanged(nameof(ApprovedCount));
            OnPropertyChanged(nameof(ClosedCount));
            OnPropertyChanged(nameof(QueueCountText));
        }
    }
}
