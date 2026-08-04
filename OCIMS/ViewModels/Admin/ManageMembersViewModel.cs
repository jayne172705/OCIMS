using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using eSureHi.Views.Admin.UserControls;

namespace eSureHi.ViewModels.Admin
{
    public class ManageMemberRow
    {
        public int RowNumber { get; init; }
        public Beneficiary Beneficiary { get; init; } = new();
        public string FamilyId { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        /// <summary>"Last, First" — used by the search suggestion popup.</summary>
        public string DisplayName { get; init; } = string.Empty;
        public string FamilyRole { get; init; } = string.Empty;
        public string Barangay { get; init; } = string.Empty;
        public string GroupEmploymentType { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
    }

    public sealed class PageNavigationItem
    {
        public int Number { get; init; }
        public bool IsCurrent { get; init; }
    }

    public class ManageMembersViewModel : ObservableObject
    {
        private const int PageSize = 10;
        private const int MaxSuggestions = 8;

        private readonly ObservableCollection<ManageMemberRow> _allMembers = new();
        private readonly List<ManageMemberRow> _filteredMembers = new();

        public ObservableCollection<ManageMemberRow> DisplayedMembers { get; } = new();
        public ObservableCollection<ManageMemberRow> SearchSuggestions { get; } = new();
        public ObservableCollection<string> GroupOptions { get; } = new();
        public ObservableCollection<string> BarangayOptions { get; } = new();
        public ObservableCollection<PageNavigationItem> PageNumbers { get; } = new();
        public string[] StatusOptions { get; } = { "All Status", "Active", "Pending", "Under Review", "Rejected", "Released", "Inactive" };

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    SetCurrentPage(1, updateResults: false);
                    ApplyFilter();
                    UpdateSuggestions();
                }
            }
        }

        // The suggestion popup floats over the table; the table keeps filtering
        // underneath so the popup is purely additive to the existing behaviour.
        private bool _isSuggestionsOpen;
        public bool IsSuggestionsOpen
        {
            get => _isSuggestionsOpen;
            set => SetProperty(ref _isSuggestionsOpen, value);
        }

        private string _selectedGroup = "All Groups";
        public string SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (SetProperty(ref _selectedGroup, value))
                {
                    SetCurrentPage(1, updateResults: false);
                    ApplyFilter();
                }
            }
        }

        private string _selectedBarangay = "All Barangays";
        public string SelectedBarangay
        {
            get => _selectedBarangay;
            set
            {
                if (SetProperty(ref _selectedBarangay, value))
                {
                    SetCurrentPage(1, updateResults: false);
                    ApplyFilter();
                }
            }
        }

        private string _selectedStatus = "All Status";
        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetProperty(ref _selectedStatus, value))
                {
                    SetCurrentPage(1, updateResults: false);
                    ApplyFilter();
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private int _totalActiveMembers;
        public int TotalActiveMembers
        {
            get => _totalActiveMembers;
            set => SetProperty(ref _totalActiveMembers, value);
        }

        private int _totalActiveDependents;
        public int TotalActiveDependents
        {
            get => _totalActiveDependents;
            set => SetProperty(ref _totalActiveDependents, value);
        }

        private int _totalMemberCount;
        public int TotalMemberCount
        {
            get => _totalMemberCount;
            set => SetProperty(ref _totalMemberCount, value);
        }

        private int _filteredCount;
        public int FilteredCount
        {
            get => _filteredCount;
            set => SetProperty(ref _filteredCount, value);
        }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            private set => SetProperty(ref _currentPage, value);
        }

        private int _totalPages = 1;
        public int TotalPages
        {
            get => _totalPages;
            private set => SetProperty(ref _totalPages, value);
        }

        private int _pageStart;
        public int PageStart
        {
            get => _pageStart;
            private set => SetProperty(ref _pageStart, value);
        }

        private int _pageEnd;
        public int PageEnd
        {
            get => _pageEnd;
            private set => SetProperty(ref _pageEnd, value);
        }

        public bool CanGoPrevious => CurrentPage > 1;
        public bool CanGoNext => CurrentPage < TotalPages;

        public bool CanUpdateMember => PermissionService.CanApproveWorkflow;

        public string PageSummaryText =>
            FilteredCount == 0
                ? "No member records found"
                : $"Showing {PageStart} to {PageEnd} of {FilteredCount} results";

        public RelayCommand RefreshCommand { get; }
        public RelayCommand ResetCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }
        public RelayCommand<ManageMemberRow> UpdateCommand { get; }
        public RelayCommand<ManageMemberRow> ViewDetailsCommand { get; }
        public RelayCommand<ManageMemberRow> OpenSuggestionCommand { get; }
        public RelayCommand PreviousPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand<PageNavigationItem> GoToPageCommand { get; }

        public ManageMembersViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            ResetCommand = new RelayCommand(ResetFilters);
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);
            UpdateCommand = new RelayCommand<ManageMemberRow>(OpenMemberForEdit, row => row is not null && CanUpdateMember);
            ViewDetailsCommand = new RelayCommand<ManageMemberRow>(OpenMemberDetail, row => row is not null);
            OpenSuggestionCommand = new RelayCommand<ManageMemberRow>(OpenMemberDetail, row => row is not null);
            PreviousPageCommand = new RelayCommand(() => SetCurrentPage(CurrentPage - 1), () => CanGoPrevious);
            NextPageCommand = new RelayCommand(() => SetCurrentPage(CurrentPage + 1), () => CanGoNext);
            GoToPageCommand = new RelayCommand<PageNavigationItem>(item =>
            {
                if (item is not null)
                    SetCurrentPage(item.Number);
            });

            GroupOptions.Add("All Groups");
            BarangayOptions.Add("All Barangays");

            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            StatusMessage = string.Empty;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var beneficiaries = await db.Beneficiaries
                    .Include(b => b.Employee)
                    .OrderBy(b => b.LastName)
                    .ThenBy(b => b.FirstName)
                    .ToListAsync();

                TotalMemberCount = beneficiaries.Count;
                TotalActiveMembers = beneficiaries.Count(b => b.IsActive);
                TotalActiveDependents = beneficiaries.Count(b => b.IsActive && !b.IsPrimary);

                var rows = beneficiaries
                    .Select((beneficiary, index) => CreateRow(beneficiary, index + 1))
                    .ToList();

                _allMembers.Clear();
                foreach (var row in rows)
                    _allMembers.Add(row);

                RefreshGroups(rows);
                RefreshBarangays(rows);
                SetCurrentPage(1, updateResults: false);
                ApplyFilter();
                UpdateSuggestions();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load members failed: {ex.GetBaseException().Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static ManageMemberRow CreateRow(Beneficiary beneficiary, int rowNumber)
        {
            var group = FirstNonEmpty(
                beneficiary.SourceOfFunds,
                beneficiary.Employee?.EmploymentType,
                "Unassigned");

            return new ManageMemberRow
            {
                RowNumber = rowNumber,
                Beneficiary = beneficiary,
                FamilyId = FirstNonEmpty(
                    beneficiary.BeneficiaryId,
                    beneficiary.CivilRegistryId,
                    beneficiary.Employee?.EmployeeNo,
                    $"BEN-{beneficiary.BenId:000000}"),
                FullName = FirstNonEmpty(beneficiary.FullName, "Unnamed Member"),
                DisplayName = BuildDisplayName(beneficiary),
                FamilyRole = BuildFamilyRole(beneficiary),
                Barangay = FirstNonEmpty(beneficiary.Employee?.Barangay, "Not set"),
                GroupEmploymentType = group,
                Status = BuildStatus(beneficiary)
            };
        }

        /// <summary>"Last, First" for the suggestion popup; falls back to whatever name we have.</summary>
        internal static string BuildDisplayName(Beneficiary beneficiary)
        {
            var last = beneficiary.LastName?.Trim() ?? string.Empty;
            var first = beneficiary.FirstName?.Trim() ?? string.Empty;

            if (last.Length > 0 && first.Length > 0)
                return $"{last}, {first}";

            return FirstNonEmpty(last, first, beneficiary.FullName, "Unnamed Member");
        }

        internal static string BuildFamilyRole(Beneficiary beneficiary) =>
            beneficiary.IsPrimary
                ? "Head of Family"
                : FirstNonEmpty(beneficiary.Relationship, "Member");

        internal static string BuildStatus(Beneficiary beneficiary)
        {
            if (!beneficiary.IsActive)
                return "Inactive";

            return beneficiary.WorkflowStatus switch
            {
                WorkflowStatuses.Approved => "Active",
                WorkflowStatuses.Pending => "Pending",
                WorkflowStatuses.UnderReview => "Under Review",
                WorkflowStatuses.Rejected => "Rejected",
                WorkflowStatuses.Released => "Released",
                _ => string.IsNullOrWhiteSpace(beneficiary.WorkflowStatus)
                    ? "Active"
                    : beneficiary.WorkflowStatus
            };
        }

        internal static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

        private void RefreshGroups(IEnumerable<ManageMemberRow> rows)
        {
            var current = _selectedGroup;

            GroupOptions.Clear();
            GroupOptions.Add("All Groups");

            foreach (var group in rows
                         .Select(row => row.GroupEmploymentType)
                         .Where(group => !string.IsNullOrWhiteSpace(group))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(group => group))
            {
                GroupOptions.Add(group);
            }

            _selectedGroup = GroupOptions.Contains(current) ? current : "All Groups";
            OnPropertyChanged(nameof(SelectedGroup));
        }

        private void RefreshBarangays(IEnumerable<ManageMemberRow> rows)
        {
            var current = _selectedBarangay;

            BarangayOptions.Clear();
            BarangayOptions.Add("All Barangays");

            foreach (var barangay in rows
                         .Select(row => row.Barangay)
                         .Where(barangay => !string.IsNullOrWhiteSpace(barangay) && !string.Equals(barangay, "Not set", StringComparison.OrdinalIgnoreCase))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(barangay => barangay))
            {
                BarangayOptions.Add(barangay);
            }

            _selectedBarangay = BarangayOptions.Contains(current) ? current : "All Barangays";
            OnPropertyChanged(nameof(SelectedBarangay));
        }

        private void ApplyFilter()
        {
            var query = _allMembers.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.Trim().ToLowerInvariant();
                query = query.Where(row =>
                    row.FullName.ToLowerInvariant().Contains(search) ||
                    row.FamilyId.ToLowerInvariant().Contains(search) ||
                    row.Barangay.ToLowerInvariant().Contains(search) ||
                    row.GroupEmploymentType.ToLowerInvariant().Contains(search));
            }

            if (SelectedGroup != "All Groups")
            {
                query = query.Where(row =>
                    string.Equals(row.GroupEmploymentType, SelectedGroup, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedBarangay != "All Barangays")
            {
                query = query.Where(row =>
                    string.Equals(row.Barangay, SelectedBarangay, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedStatus != "All Status")
            {
                query = query.Where(row =>
                    string.Equals(row.Status, SelectedStatus, StringComparison.OrdinalIgnoreCase));
            }

            _filteredMembers.Clear();
            _filteredMembers.AddRange(query);

            FilteredCount = _filteredMembers.Count;
            TotalPages = Math.Max(1, (int)Math.Ceiling(Math.Max(FilteredCount, 1) / (double)PageSize));

            if (CurrentPage > TotalPages)
                CurrentPage = TotalPages;
            if (CurrentPage < 1)
                CurrentPage = 1;

            RefreshDisplayedMembers();
        }

        /// <summary>
        /// Rebuilds the name-suggestion list from the in-memory roster. Matching is
        /// name-first (the popup is a name picker) with beneficiary ID as a fallback
        /// so the hint text stays honest.
        /// </summary>
        private void UpdateSuggestions()
        {
            SearchSuggestions.Clear();

            var search = SearchText?.Trim();
            if (string.IsNullOrWhiteSpace(search))
            {
                var defaultMatches = _allMembers
                    .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Take(MaxSuggestions);
                foreach (var match in defaultMatches)
                    SearchSuggestions.Add(match);

                IsSuggestionsOpen = SearchSuggestions.Count > 0;
                return;
            }

            var needle = search.ToLowerInvariant();
            var matches = _allMembers
                .Where(row =>
                    row.FullName.ToLowerInvariant().Contains(needle) ||
                    row.DisplayName.ToLowerInvariant().Contains(needle) ||
                    row.FamilyId.ToLowerInvariant().Contains(needle))
                .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(MaxSuggestions);

            foreach (var match in matches)
                SearchSuggestions.Add(match);

            IsSuggestionsOpen = SearchSuggestions.Count > 0;
        }

        private void RefreshDisplayedMembers()
        {
            DisplayedMembers.Clear();

            if (_filteredMembers.Count == 0)
            {
                PageStart = 0;
                PageEnd = 0;
                BuildPageLinks();
                UpdatePagingState();
                return;
            }

            var skip = (CurrentPage - 1) * PageSize;
            var pageItems = _filteredMembers
                .Skip(skip)
                .Take(PageSize)
                .ToList();

            var rowNumber = skip + 1;
            foreach (var row in pageItems)
            {
                DisplayedMembers.Add(new ManageMemberRow
                {
                    RowNumber = rowNumber++,
                    Beneficiary = row.Beneficiary,
                    FamilyId = row.FamilyId,
                    FullName = row.FullName,
                    DisplayName = row.DisplayName,
                    FamilyRole = row.FamilyRole,
                    Barangay = row.Barangay,
                    GroupEmploymentType = row.GroupEmploymentType,
                    Status = row.Status
                });
            }

            PageStart = skip + 1;
            PageEnd = skip + pageItems.Count;

            BuildPageLinks();
            UpdatePagingState();
        }

        private void BuildPageLinks()
        {
            PageNumbers.Clear();

            if (FilteredCount == 0)
                return;

            var start = Math.Max(1, CurrentPage - 4);
            var end = Math.Min(TotalPages, start + 9);
            start = Math.Max(1, end - 9);

            for (var page = start; page <= end; page++)
            {
                PageNumbers.Add(new PageNavigationItem
                {
                    Number = page,
                    IsCurrent = page == CurrentPage
                });
            }
        }

        private void UpdatePagingState()
        {
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(PageSummaryText));
            PreviousPageCommand.RaiseCanExecuteChanged();
            NextPageCommand.RaiseCanExecuteChanged();
        }

        private void SetCurrentPage(int page, bool updateResults = true)
        {
            var resolvedPage = Math.Max(1, page);
            if (CurrentPage == resolvedPage)
            {
                if (updateResults)
                    RefreshDisplayedMembers();
                return;
            }

            CurrentPage = resolvedPage;

            if (updateResults)
                RefreshDisplayedMembers();
        }

        private void ResetFilters()
        {
            _searchText = string.Empty;
            _selectedGroup = "All Groups";
            _selectedBarangay = "All Barangays";
            _selectedStatus = "All Status";

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedGroup));
            OnPropertyChanged(nameof(SelectedBarangay));
            OnPropertyChanged(nameof(SelectedStatus));

            SearchSuggestions.Clear();
            IsSuggestionsOpen = false;

            SetCurrentPage(1, updateResults: false);
            ApplyFilter();
        }

        private static void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new HomeView());
        }

        /// <summary>
        /// Read-only, full-screen profile. Used by both the suggestion popup and the
        /// table's "View Details" button, which previously shared the edit path.
        /// </summary>
        private void OpenMemberDetail(ManageMemberRow? row)
        {
            if (row is null)
                return;

            IsSuggestionsOpen = false;
            NavigationService.Instance.NavigateTo(new MemberDetailView(row.Beneficiary));
        }

        private static void OpenMemberForEdit(ManageMemberRow? row)
        {
            if (row is null)
                return;

            var viewModel = new BeneficiaryStagingViewModel
            {
                SelectedSource = "Insurance Beneficiaries",
                SelectedSystemBeneficiary = row.Beneficiary
            };

            NavigationService.Instance.NavigateTo(new BeneficiariesView(viewModel, openInitialSearch: false));
        }
    }
}
