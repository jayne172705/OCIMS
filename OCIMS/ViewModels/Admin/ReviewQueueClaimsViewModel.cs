using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;

namespace eSureHi.ViewModels.Admin
{
    public class ReviewQueueClaimsViewModel : ObservableObject
    {
        private readonly ObservableCollection<Claim> _all = new();
        private readonly int? _initialClaimId;

        public ObservableCollection<Claim> QueueClaims { get; } = new();

        public GridLength BrowseColumnWidth { get; }
        public GridLength BrowseSeparatorWidth { get; }
        public bool IsBrowseVisible { get; }

        private Claim? _selectedClaim;
        public Claim? SelectedClaim
        {
            get => _selectedClaim;
            set
            {
                if (SetProperty(ref _selectedClaim, value))
                    _ = LoadSelectedDetailAsync();
            }
        }

        private ClaimDetailViewModel? _selectedDetail;
        public ClaimDetailViewModel? SelectedDetail
        {
            get => _selectedDetail;
            set
            {
                if (SetProperty(ref _selectedDetail, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                    OnPropertyChanged(nameof(HasNoSelection));
                    OnPropertyChanged(nameof(PositionText));
                }
            }
        }

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

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool HasSelection => SelectedDetail?.Claim is not null;
        public bool HasNoSelection => !HasSelection;
        public int TotalCount => QueueClaims.Count;
        public string PositionText
        {
            get
            {
                if (SelectedClaim is null || QueueClaims.Count == 0)
                    return $"0 / {QueueClaims.Count}";

                var index = QueueClaims.IndexOf(SelectedClaim) + 1;
                return $"{index} / {QueueClaims.Count}";
            }
        }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand PreviousCommand { get; }
        public RelayCommand NextCommand { get; }

        public ReviewQueueClaimsViewModel(int? initialClaimId = null, bool detailOnly = false)
        {
            _initialClaimId = initialClaimId;
            IsBrowseVisible = !detailOnly;
            BrowseColumnWidth = detailOnly ? new GridLength(0) : new GridLength(360);
            BrowseSeparatorWidth = detailOnly ? new GridLength(0) : new GridLength(12);

            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            PreviousCommand = new RelayCommand(SelectPrevious, () => QueueClaims.Count > 0 && SelectedClaim is not null && QueueClaims.IndexOf(SelectedClaim) > 0);
            NextCommand = new RelayCommand(SelectNext, () => QueueClaims.Count > 0 && SelectedClaim is not null && QueueClaims.IndexOf(SelectedClaim) < QueueClaims.Count - 1);

            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var selectedId = SelectedClaim?.ClaimId ?? _initialClaimId;

                using var db = eSureHiDbContextFactory.Create();
                var claims = await db.Claims
                    .Include(c => c.Employee).ThenInclude(e => e!.Department)
                    .Include(c => c.Policy)
                    .Where(c => c.ClaimStatus == "Submitted" ||
                                c.ClaimStatus == "Under Review" ||
                                c.ClaimStatus == "Approved" ||
                                c.ClaimStatus == "Partially Approved")
                    .OrderBy(c => c.ClaimStatus == "Submitted" ? 0 :
                                  c.ClaimStatus == "Under Review" ? 1 : 2)
                    .ThenByDescending(c => c.SubmittedDate ?? c.CreatedAt)
                    .ToListAsync();

                _all.Clear();
                foreach (var claim in claims)
                    _all.Add(claim);

                ApplyFilter();

                SelectedClaim = QueueClaims.FirstOrDefault(c => c.ClaimId == selectedId)
                    ?? QueueClaims.FirstOrDefault();
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

        private async Task LoadSelectedDetailAsync()
        {
            if (SelectedClaim is null)
            {
                SelectedDetail = null;
                RaiseNavigationState();
                return;
            }

            var detail = new ClaimDetailViewModel
            {
                OnStatusChanged = async () => await LoadAsync()
            };
            await detail.LoadAsync(SelectedClaim.ClaimId);
            if (detail.AmountApproved <= 0 && detail.Claim is not null)
                detail.AmountApproved = detail.Claim.AmountClaimed;

            SelectedDetail = detail;
            RaiseNavigationState();
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

            QueueClaims.Clear();
            foreach (var claim in query)
                QueueClaims.Add(claim);

            if (SelectedClaim is not null && !QueueClaims.Any(c => c.ClaimId == SelectedClaim.ClaimId))
                SelectedClaim = QueueClaims.FirstOrDefault();

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(PositionText));
            RaiseNavigationState();
        }

        private void SelectPrevious()
        {
            if (SelectedClaim is null)
                return;

            var index = QueueClaims.IndexOf(SelectedClaim);
            if (index > 0)
                SelectedClaim = QueueClaims[index - 1];
        }

        private void SelectNext()
        {
            if (SelectedClaim is null)
                return;

            var index = QueueClaims.IndexOf(SelectedClaim);
            if (index >= 0 && index < QueueClaims.Count - 1)
                SelectedClaim = QueueClaims[index + 1];
        }

        private void RaiseNavigationState()
        {
            PreviousCommand.RaiseCanExecuteChanged();
            NextCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(HasNoSelection));
            OnPropertyChanged(nameof(PositionText));
        }
    }
}
