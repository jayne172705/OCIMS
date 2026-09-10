using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using eSureHi.Views.Admin.UserControls;

namespace eSureHi.ViewModels.Admin
{
    public class FamilyMemberDisplay
    {
        public string FullName { get; init; } = string.Empty;
        public string FamilyRole { get; init; } = string.Empty;
        public string RegistrationStatus { get; init; } = string.Empty;
        public string RegistrationStatusBackground { get; init; } = "#F1F5F9";
        public string RegistrationStatusForeground { get; init; } = "#475569";
        public string CivilRegistryId { get; init; } = string.Empty;
        public string BeneficiaryId { get; init; } = string.Empty;
    }

    /// <summary>
    /// One row in the live-search suggestion popup. Property names deliberately match
    /// <c>ManageMemberRow</c> so both pages share Views/Shared/SuggestionPopupResources.xaml.
    /// Carries the underlying record so picking a suggestion sets exactly the same
    /// selection the corresponding ListBox row would.
    /// </summary>
    public class BeneficiarySuggestionRow
    {
        public string DisplayName { get; init; } = string.Empty;
        public string FamilyId { get; init; } = string.Empty;
        public string FamilyRole { get; init; } = string.Empty;
        public string Barangay { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;

        /// <summary>Set for CRS/master-list suggestions.</summary>
        public BeneficiaryStaging? Record { get; init; }

        /// <summary>Set for insurance-beneficiary suggestions.</summary>
        public Beneficiary? Beneficiary { get; init; }

        public static BeneficiarySuggestionRow FromRecord(BeneficiaryStaging record) => new()
        {
            DisplayName = BuildName(record.LastName, record.FirstName, record.DisplayName),
            FamilyId = Or(record.BeneficiaryId, record.CivilRegistryId, record.ResidentsId?.ToString(), "No ID"),
            FamilyRole = Or(record.ResidentSearchRoleBadge, "Unclassified"),
            // CRS staging carries a single free-text address rather than a barangay column.
            Barangay = Or(record.Address, "No address"),
            Status = Or(record.LinkStatus, "Unlinked"),
            Record = record
        };

        public static BeneficiarySuggestionRow FromBeneficiary(Beneficiary beneficiary) => new()
        {
            DisplayName = BuildName(beneficiary.LastName, beneficiary.FirstName, beneficiary.FullName),
            FamilyId = Or(beneficiary.BeneficiaryId, beneficiary.CivilRegistryId,
                          beneficiary.Employee?.EmployeeNo, $"BEN-{beneficiary.BenId:000000}"),
            FamilyRole = beneficiary.IsPrimary ? "Head of Family" : Or(beneficiary.Relationship, "Member"),
            Barangay = Or(beneficiary.Employee?.Barangay, "No barangay"),
            Status = Or(beneficiary.WorkflowStatus, "Pending"),
            Beneficiary = beneficiary
        };

        private static string BuildName(string? last, string? first, string? fallback)
        {
            var l = last?.Trim() ?? string.Empty;
            var f = first?.Trim() ?? string.Empty;

            if (l.Length > 0 && f.Length > 0)
                return $"{l}, {f}";

            return Or(l, f, fallback, "Unnamed");
        }

        private static string Or(params string?[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                    return candidate.Trim();
            }

            return string.Empty;
        }
    }

    public class BeneficiaryStagingViewModel : ObservableObject
    {
        // ── Import State ───────────────────────────────────────────────
        private bool _isImporting;
        private int _importProgress;
        private int _importTotal;
        private string _importMessage = string.Empty;

        public bool IsImporting
        {
            get => _isImporting;
            set
            {
                SetProperty(ref _isImporting, value);
                ImportCommand.RaiseCanExecuteChanged();
                CancelImportCommand.RaiseCanExecuteChanged();
            }
        }
        public int ImportProgress
        {
            get => _importProgress;
            set => SetProperty(ref _importProgress, value);
        }
        public int ImportTotal
        {
            get => _importTotal;
            set => SetProperty(ref _importTotal, value);
        }
        public string ImportMessage
        {
            get => _importMessage;
            set => SetProperty(ref _importMessage, value);
        }
        public double ImportPercent =>
            ImportTotal > 0 ? (double)ImportProgress / ImportTotal * 100 : 0;

        // ── Staging Data ───────────────────────────────────────────────
        private ObservableCollection<BeneficiaryStaging> _allRecords = new();
        public ObservableCollection<BeneficiaryStaging> DisplayedRecords { get; } = new();
        private ObservableCollection<Beneficiary> _allSystemBeneficiaries = new();
        public ObservableCollection<Beneficiary> DisplayedSystemBeneficiaries { get; } = new();
        public ObservableCollection<InsurancePolicy> InsurancePolicies { get; } = new();

        // ── Live search suggestions ────────────────────────────────────
        // Purely additive: the popup mirrors the top of whichever list is already
        // showing, so it can never disagree with the ListBox underneath.
        private const int MaxSuggestions = 8;

        public ObservableCollection<BeneficiarySuggestionRow> SearchSuggestions { get; } = new();

        private bool _isSuggestionsOpen;
        public bool IsSuggestionsOpen
        {
            get => _isSuggestionsOpen;
            set => SetProperty(ref _isSuggestionsOpen, value);
        }

        // Only a keystroke in the search box should raise the popup. ApplyFilter also
        // runs for source/status changes and after loads, which must not pop it open.
        private bool _suggestionsRequested;

        public ObservableCollection<Employee> Employees { get; } = new();
        public ObservableCollection<Employee> FilteredEmployees { get; } = new();
        public ObservableCollection<FamilyMemberDisplay> FamilyMembers { get; } = new();

        private string _employeeSearch = string.Empty;
        public string EmployeeSearch
        {
            get => _employeeSearch;
            set
            {
                SetProperty(ref _employeeSearch, value);
                ApplyEmployeeFilter();
            }
        }

        private FamilyMemberDisplay? _selectedFamilyMember;
        public FamilyMemberDisplay? SelectedFamilyMember
        {
            get => _selectedFamilyMember;
            set
            {
                if (SetProperty(ref _selectedFamilyMember, value) && value is not null)
                {
                    _ = OpenFamilyMemberProfileAsync(value);
                }
            }
        }

        private void ApplyEmployeeFilter()
        {
            FilteredEmployees.Clear();
            var s = _employeeSearch.Trim().ToLower();
            var source = string.IsNullOrWhiteSpace(s)
                ? Employees
                : new System.Collections.ObjectModel.ObservableCollection<Employee>(
                    Employees.Where(e =>
                        e.FullName.ToLower().Contains(s) ||
                        e.EmployeeNo.ToLower().Contains(s)));

            foreach (var e in source) FilteredEmployees.Add(e);
        }

        private BeneficiaryStaging? _selectedRecord;
        public BeneficiaryStaging? SelectedRecord
        {
            get => _selectedRecord;
            set
            {
                SetProperty(ref _selectedRecord, value);
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(IsProfileOpen));
                OnPropertyChanged(nameof(ShowRegistrationButtons));
                LinkCommand.RaiseCanExecuteChanged();
                SkipCommand.RaiseCanExecuteChanged();
                RemoveBeneficiaryCommand.RaiseCanExecuteChanged();
                if (!_selectionOnly && value is not null)
                    _ = OpenProfileAsync(value);
            }
        }
        public bool HasSelection => SelectedRecord is not null;
        public bool IsProfileOpen => SelectedRecord is not null;
        public bool IsQualifiedForInsurance =>
            CanAddBeneficiary;
        public bool CanAddBeneficiary =>
            SelectedRecord is not null;

        public bool ShowRegistrationButtons =>
            SelectedRecord is not null && SelectedRecord.LinkStatus != "Linked";

        public string AddMemberButtonText =>
            PermissionService.CanApproveWorkflow
                ? "ADD TO INSURANCE"
                : "SUBMIT FOR APPROVAL";

        // Role-level gate for whether approval/review/release controls render at all.
        // User-role registrars submit members for approval (Pending) and never see confirm controls.
        public bool ShowApprovalControls => PermissionService.CanApproveWorkflow;

        public bool CanReview =>
            PermissionService.CanApproveWorkflow &&
            SelectedSystemBeneficiary != null &&
            SelectedSystemBeneficiary.WorkflowStatus == WorkflowStatuses.Pending;

        public bool CanApproveMember =>
            PermissionService.CanApproveWorkflow &&
            SelectedSystemBeneficiary != null &&
            (SelectedSystemBeneficiary.WorkflowStatus is WorkflowStatuses.Pending or WorkflowStatuses.UnderReview ||
             !SelectedSystemBeneficiary.IsAdminConfirmed);

        public bool CanReject => 
            PermissionService.CanApproveWorkflow &&
            SelectedSystemBeneficiary != null && 
            (SelectedSystemBeneficiary.WorkflowStatus == WorkflowStatuses.Pending || 
             SelectedSystemBeneficiary.WorkflowStatus == WorkflowStatuses.UnderReview);

        public bool CanReleaseBenefit => 
            PermissionService.CanApproveWorkflow &&
            SelectedSystemBeneficiary != null && 
            SelectedSystemBeneficiary.WorkflowStatus == WorkflowStatuses.Approved;

        public bool CanRemoveBeneficiary =>
            SelectedSystemBeneficiary is not null ||
            ProfileBeneficiary is not null ||
            SelectedRecord?.LinkedBenId is not null;

        public bool CanCreateAccount =>
            SelectedSystemBeneficiary is not null || ProfileBeneficiary is not null;

        private string _statusRemarks = string.Empty;
        public string StatusRemarks
        {
            get => _statusRemarks;
            set => SetProperty(ref _statusRemarks, value);
        }

        private Beneficiary? _selectedSystemBeneficiary;
        private bool _isSyncingSystemBeneficiarySelection;
        public Beneficiary? SelectedSystemBeneficiary
        {
            get => _selectedSystemBeneficiary;
            set
            {
                if (!SetProperty(ref _selectedSystemBeneficiary, value))
                    return;

                RefreshCanExecute();
                OnPropertyChanged(nameof(CanApproveMember));
                OnPropertyChanged(nameof(CanRemoveBeneficiary));
                OnPropertyChanged(nameof(CanCreateAccount));

                if (_isSyncingSystemBeneficiarySelection || value is null)
                    return;

                if (SelectedRecord?.LinkedBenId == value.BenId)
                    return;

                _isSyncingSystemBeneficiarySelection = true;
                try
                {
                    SelectedRecord = new BeneficiaryStaging
                    {
                        BeneficiaryId = value.BeneficiaryId ?? $"IMS-BEN-{value.BenId:000000}",
                        CivilRegistryId = value.CivilRegistryId,
                        FirstName = value.FirstName,
                        LastName = value.LastName,
                        FullName = value.FullName,
                        DateOfBirth = value.DateOfBirth?.ToString("yyyy-MM-dd"),
                        Sex = value.Gender,
                        LinkedEmpId = value.EmpId,
                        LinkedBenId = value.BenId,
                        LinkStatus = "Linked"
                    };
                    Relationship = string.IsNullOrWhiteSpace(value.Relationship) ? "Other" : value.Relationship;
                    CedulaNo = value.CedulaNo ?? string.Empty;
                    Received = value.Received;
                    Contribution = value.Contribution;
                    SourceOfFunds = value.SourceOfFunds ?? SourceOfFunds;
                }
                finally
                {
                    _isSyncingSystemBeneficiarySelection = false;
                }
            }
        }

        // ── Link Fields ────────────────────────────────────────────────
        private Employee? _selectedEmployee;
        private string _relationship = "Other";
        private bool _isPrimary = false;
        private string _recipientsInsurance = string.Empty;
        private string _cedulaNo = string.Empty;
        private bool _received;
        private decimal _contribution;
        private string _sourceOfFunds = "Job Order";

        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                if (SetProperty(ref _selectedEmployee, value))
                {
                    ErrorMessage = string.Empty;
                    if (SelectedRecord is not null)
                    {
                        if (value is null)
                            BuildRequirements(SelectedRecord);
                        else
                            _ = LoadSelectedEmployeeContextAsync();
                    }
                }
            }
        }
        public string Relationship
        {
            get => _relationship;
            set => SetProperty(ref _relationship, value);
        }
        public bool IsPrimary
        {
            get => _isPrimary;
            set => SetProperty(ref _isPrimary, value);
        }

        public string RecipientsInsurance
        {
            get => _recipientsInsurance;
            set
            {
                if (SetProperty(ref _recipientsInsurance, value))
                {
                    RefreshQualificationState();
                }
            }
        }

        public string CedulaNo
        {
            get => _cedulaNo;
            set => SetProperty(ref _cedulaNo, value);
        }

        public bool Received
        {
            get => _received;
            set => SetProperty(ref _received, value);
        }

        public decimal Contribution
        {
            get => _contribution;
            set => SetProperty(ref _contribution, value);
        }

        public string[] RelationshipOptions { get; } =
            { "Spouse", "Son", "Daughter", "Child", "Parent", "Sibling", "Other" };
        public ObservableCollection<string> SourceOfFundsOptions { get; } = new()
        {
            "Job Order",
            "Casual",
            "Regular"
        };
        public string SourceOfFunds
        {
            get => _sourceOfFunds;
            set
            {
                if (SetProperty(ref _sourceOfFunds, value))
                {
                    if (!_isLoadingProfile && !_isSyncingSystemBeneficiarySelection)
                    {
                        Contribution = GetDefaultMonthlyContribution(value);
                    }
                }
            }
        }

        private static decimal GetDefaultMonthlyContribution(string? programType) =>
            programType?.Trim() switch
            {
                "Job Order" => 50m,
                "Casual" => 100m,
                "Regular" => 200m,
                _ => 0m
            };

        // ── Filters ────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        private string _statusFilter = "All";

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (!SetProperty(ref _searchText, value))
                    return;

                _suggestionsRequested = true;

                if (SelectionOnly)
                    QueueSelectionSearch();
                else
                    QueueFilter();
            }
        }
        public string StatusFilter
        {
            get => _statusFilter;
            set { SetProperty(ref _statusFilter, value); ApplyFilter(); }
        }

        public string[] StatusOptions { get; } =
            { "All", "Unlinked", "Linked", "Skipped" };

        public string[] SourceOptions { get; } =
            { "CRS Master List", "Insurance Beneficiaries" };

        private string _selectedSource = "CRS Master List";
        public string SelectedSource
        {
            get => _selectedSource;
            set
            {
                if (SetProperty(ref _selectedSource, value))
                {
                    CloseProfile();
                    if (SelectionOnly)
                    {
                        QueueSelectionSearch();
                    }
                    else
                    {
                        ApplyFilter();
                        _ = LoadAsync();
                    }
                    OnPropertyChanged(nameof(IsCrsSource));
                    OnPropertyChanged(nameof(IsSystemSource));
                    OnPropertyChanged(nameof(HasSelection));
                    OnPropertyChanged(nameof(IsProfileOpen));
                    OnPropertyChanged(nameof(ShowRegistrationButtons));
                    OnPropertyChanged(nameof(CanApproveMember));
                    OnPropertyChanged(nameof(SourceCaption));
                    OnPropertyChanged(nameof(ActiveTotalCount));
                    OnPropertyChanged(nameof(ActiveFilteredCount));
                }
            }
        }

        public bool IsCrsSource => SelectedSource == "CRS Master List";
        public bool IsSystemSource => !IsCrsSource;
        public string SourceCaption => "CRS records from the configured CRS connection";
        public int ActiveTotalCount => IsCrsSource ? TotalCount : SystemTotalCount;
        public int ActiveFilteredCount => IsCrsSource ? FilteredCount : SystemFilteredCount;

        // ── Counts ─────────────────────────────────────────────────────
        private int _totalCount;
        private int _filteredCount;
        private int _unlinkedCount;
        private int _linkedCount;
        private int _systemTotalCount;
        private int _systemFilteredCount;

        public int TotalCount { get => _totalCount; set { SetProperty(ref _totalCount, value); OnPropertyChanged(nameof(ActiveTotalCount)); } }
        public int FilteredCount { get => _filteredCount; set { SetProperty(ref _filteredCount, value); OnPropertyChanged(nameof(ActiveFilteredCount)); } }
        public int UnlinkedCount { get => _unlinkedCount; set => SetProperty(ref _unlinkedCount, value); }
        public int LinkedCount { get => _linkedCount; set => SetProperty(ref _linkedCount, value); }
        public int SystemTotalCount { get => _systemTotalCount; set { SetProperty(ref _systemTotalCount, value); OnPropertyChanged(nameof(ActiveTotalCount)); } }
        public int SystemFilteredCount { get => _systemFilteredCount; set { SetProperty(ref _systemFilteredCount, value); OnPropertyChanged(nameof(ActiveFilteredCount)); } }

        public ObservableCollection<Benefit> ProfileBenefits { get; } = new();
        public ObservableCollection<Premium> ProfilePremiums { get; } = new();
        public ObservableCollection<DocumentTransaction> ProfileTransactions { get; } = new();
        public ObservableCollection<Cedula> ProfileCedulas { get; } = new();
        public ObservableCollection<RequirementStatus> ProfileRequirements { get; } = new();

        private Beneficiary? _profileBeneficiary;
        private Employee? _profileEmployee;
        private string _profileStatus = "Not validated";
        private string _profileCedulaStatus = "No cedula payment found";
        private string _profileTransactionStatus = "No transactions found";
        private string _profileAddress = string.Empty;
        private string _eligibilityStatus = "Reviewing qualification...";

        public Beneficiary? ProfileBeneficiary
        {
            get => _profileBeneficiary;
            set
            {
                if (SetProperty(ref _profileBeneficiary, value))
                {
                    OnPropertyChanged(nameof(CanRemoveBeneficiary));
                    OnPropertyChanged(nameof(CanCreateAccount));
                    RemoveBeneficiaryCommand.RaiseCanExecuteChanged();
                    CreateAccountCommand.RaiseCanExecuteChanged();
                }
            }
        }
        public Employee? ProfileEmployee { get => _profileEmployee; set => SetProperty(ref _profileEmployee, value); }
        public string ProfileStatus { get => _profileStatus; set => SetProperty(ref _profileStatus, value); }
        public string ProfileCedulaStatus { get => _profileCedulaStatus; set => SetProperty(ref _profileCedulaStatus, value); }
        public string ProfileTransactionStatus { get => _profileTransactionStatus; set => SetProperty(ref _profileTransactionStatus, value); }
        public StatusIndicator GgmsSyncStatus { get; } = new();
        public string ProfileAddress { get => _profileAddress; set => SetProperty(ref _profileAddress, value); }
        public string EligibilityStatus { get => _eligibilityStatus; set => SetProperty(ref _eligibilityStatus, value); }
        private string _qualificationReason = "Select a beneficiary to review qualification.";
        public string QualificationReason { get => _qualificationReason; set => SetProperty(ref _qualificationReason, value); }

        // ── State ──────────────────────────────────────────────────────
        private bool _isLoading;
        private bool _isLoadingProfile;
        private string _errorMessage = string.Empty;
        private bool _hasAutoSyncedCrs;

        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand ImportCommand { get; }
        public RelayCommand CancelImportCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand SearchCommand { get; }
        public RelayCommand LinkCommand { get; }
        public RelayCommand SkipCommand { get; }
        public RelayCommand ClearFilterCommand { get; }
        public RelayCommand BackToSearchCommand { get; }
        public RelayCommand StartReviewCommand { get; }
        public RelayCommand ApproveMemberCommand { get; }
        public RelayCommand RejectCommand { get; }
        public RelayCommand ReleaseBenefitCommand { get; }
        public RelayCommand ViewTransactionsCommand { get; }
        public RelayCommand RemoveBeneficiaryCommand { get; }
        public RelayCommand CreateAccountCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }
        public RelayCommand<BeneficiarySuggestionRow> OpenSuggestionCommand { get; }

        private CancellationTokenSource? _cts;
        private bool _selectionOnly;
        public bool SelectionOnly
        {
            get => _selectionOnly;
            set
            {
                if (SetProperty(ref _selectionOnly, value))
                {
                    if (_selectionOnly)
                    {
                        QueueSelectionSearch();
                    }
                    else
                    {
                        ApplyFilter();
                        if (SelectedRecord is not null)
                        {
                            _ = OpenProfileAsync(SelectedRecord);
                        }
                    }
                }
            }
        }
        private CancellationTokenSource? _selectionSearchCts;
        private CancellationTokenSource? _filterCts;

        private const int FilterDebounceMs = 180;

        /// <summary>
        /// Ceiling on rows pushed into the bound result collections. Matches the Take(80)
        /// the selection-only query already applies. Without it a one-character search on
        /// a full CRS import raises ~40,000 CollectionChanged notifications on the UI
        /// thread; FilteredCount still reports the true match count, so nothing is hidden.
        /// </summary>
        private const int MaxDisplayedRows = 80;

        // ── Constructor ────────────────────────────────────────────────
        public BeneficiaryStagingViewModel(bool selectionOnly = false)
        {
            _selectionOnly = selectionOnly;
            ImportCommand = new RelayCommand(async () => await StartImportAsync(),
                                      () => !IsImporting);
            CancelImportCommand = new RelayCommand(CancelImport,
                                      () => IsImporting);
            RefreshCommand = new RelayCommand(async () => 
        {
            await AutoSyncCrsMasterListAsync(force: true);
            await LoadAsync();
        });
            SearchCommand = new RelayCommand(ApplyFilter);
            LinkCommand = new RelayCommand(async () => await LinkAsync(),
                                      () => CanAddBeneficiary);
            SkipCommand = new RelayCommand(async () => await SkipAsync(),
                                      () => SelectedRecord is not null &&
                                            SelectedRecord.LinkStatus == "Unlinked");
            
            StartReviewCommand = new RelayCommand(async () => await UpdateStatusAsync(WorkflowStatuses.UnderReview), () => CanReview);
            ApproveMemberCommand = new RelayCommand(async () => await UpdateStatusAsync(WorkflowStatuses.Approved), () => CanApproveMember);
            RejectCommand = new RelayCommand(async () => await UpdateStatusAsync(WorkflowStatuses.Rejected), () => CanReject);
            ReleaseBenefitCommand = new RelayCommand(async () => await UpdateStatusAsync(WorkflowStatuses.Released), () => CanReleaseBenefit);
            ViewTransactionsCommand = new RelayCommand(ShowTransactions);
            RemoveBeneficiaryCommand = new RelayCommand(async () => await RemoveBeneficiaryAsync(), () => CanRemoveBeneficiary);
            CreateAccountCommand = new RelayCommand(OpenCreateAccountDialog, () => CanCreateAccount);
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);

            ClearFilterCommand = new RelayCommand(ClearFilters);
            BackToSearchCommand = new RelayCommand(CloseProfile);
            OpenSuggestionCommand = new RelayCommand<BeneficiarySuggestionRow>(ApplySuggestion, row => row is not null);

            if (SelectionOnly)
            {
                _ = LoadSelectionRecordsAsync();
            }
            else
            {
                _ = LoadAsync();
                _ = LoadEmployeesAsync();
                _ = LoadInsurancePoliciesAsync();
                _ = LoadSourceFundsAsync();
            }
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new HomeView());
        }

        private InsurancePolicy? _selectedPolicy;
        public InsurancePolicy? SelectedPolicy
        {
            get => _selectedPolicy;
            set
            {
                if (SetProperty(ref _selectedPolicy, value))
                {
                    RecipientsInsurance = value?.PolicyName ?? string.Empty;
                }
            }
        }

        private async Task UpdateStatusAsync(string status)
        {
            if (SelectedSystemBeneficiary == null) return;

            if (status != WorkflowStatuses.Pending && !PermissionService.CanApproveWorkflow)
            {
                MessageBox.Show("You do not have permission to modify member workflow status.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var beneficiary = await db.Beneficiaries
                    .FirstOrDefaultAsync(b => b.BenId == SelectedSystemBeneficiary.BenId);

                if (beneficiary is null)
                {
                    MessageBox.Show("Beneficiary not found.", "Workflow Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                beneficiary.WorkflowStatus = status;
                beneficiary.StatusRemarks = StatusRemarks;
                bool isConfirming = false;
                if (status == WorkflowStatuses.Approved)
                {
                    beneficiary.IsAdminConfirmed = true;
                    isConfirming = true;
                }
                await db.SaveChangesAsync();

                StatusRemarks = string.Empty;
                await LoadAsync();

                if (SelectedRecord != null)
                    await OpenProfileAsync(SelectedRecord);

                RefreshCanExecute();

                if (isConfirming)
                {
                    MessageBox.Show("Beneficiary confirmation successful. The record has been marked as confirmed.", 
                                    "Member Confirmed", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetBaseException().Message, "Workflow Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowTransactions()
        {
            if (SelectedRecord == null) return;
            var search = SelectedRecord.FullName ?? SelectedRecord.DisplayName;
            var transactionsVm = new TransactionsViewModel { SearchText = search };
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.TransactionsView(transactionsVm));
        }

        private void RefreshCanExecute()
        {
            LinkCommand.RaiseCanExecuteChanged();
            StartReviewCommand.RaiseCanExecuteChanged();
            ApproveMemberCommand.RaiseCanExecuteChanged();
            RejectCommand.RaiseCanExecuteChanged();
            ReleaseBenefitCommand.RaiseCanExecuteChanged();
            RemoveBeneficiaryCommand.RaiseCanExecuteChanged();
            CreateAccountCommand.RaiseCanExecuteChanged();
        }

        private void OpenCreateAccountDialog()
        {
            var beneficiary = SelectedSystemBeneficiary ?? ProfileBeneficiary;
            if (beneficiary is null)
            {
                ErrorMessage = "Select an insurance beneficiary first.";
                return;
            }

            var dialog = new Views.Admin.Dialogs.CreateUserAccountDialog(
                beneficiary.BenId,
                beneficiary.FullName,
                isBeneficiary: true);
            if (App.ActiveShell is not null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;
            dialog.SetSaveCallback(() => ErrorMessage = "Beneficiary account created.");
            dialog.ShowDialog();
        }

        private async Task LoadSourceFundsAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var names = (await db.SourceFunds
                    .Where(f => f.Status == "Active" &&
                                (f.FundType == "Employee Track" ||
                                 f.FundName == "Job Order" ||
                                 f.FundName == "Casual" ||
                                 f.FundName == "Regular"))
                    .Select(f => f.FundName)
                    .ToListAsync())
                    .OrderBy(GetSourceFundSortOrder)
                    .ToList();

                if (!names.Any())
                    return;

                SourceOfFundsOptions.Clear();
                foreach (var name in names)
                    SourceOfFundsOptions.Add(name);

                if (!SourceOfFundsOptions.Contains(SourceOfFunds))
                    SourceOfFunds = SourceOfFundsOptions.FirstOrDefault() ?? "Job Order";
            }
            catch
            {
                // Keep built-in defaults if the new source_funds table is not ready yet.
            }
        }

        // ── Import ─────────────────────────────────────────────────────
        private static int GetSourceFundSortOrder(string name) => name switch
        {
            "Job Order" => 0,
            "Casual" => 1,
            "Regular" => 2,
            _ => 3
        };

        private async Task StartImportAsync()
        {
            if (IsImporting) return;

            IsImporting = true;
            ImportProgress = 0;
            ImportTotal = 0;
            ImportMessage = "Connecting to CRS...";

            _cts = new CancellationTokenSource();

            var progressHandler = new Progress<CrsImportProgress>(p =>
            {
                ImportProgress = p.Imported;
                ImportTotal = p.Total > 0 ? p.Total : ImportTotal;
                ImportMessage = p.Message;
                OnPropertyChanged(nameof(ImportPercent));

                if (!p.IsRunning)
                {
                    IsImporting = false;
                    _ = LoadAsync();
                }
            });

            try
            {
                var crsConfig = Data.SharedDatabaseConfiguration.LoadCrs();
                var connStr = crsConfig.ToConnectionString();
                await CrsImportService.ImportAsync(
                    connStr, progressHandler, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                ImportMessage = "Import cancelled.";
                IsImporting = false;
            }
            catch (Exception ex)
            {
                ImportMessage = $"Import failed: {ex.Message}";
                IsImporting = false;
            }
        }

        private void CancelImport()
        {
            _cts?.Cancel();
            ImportMessage = "Cancelling...";
        }

        // ── Load ───────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                if (IsSystemSource)
                {
                    await LoadSystemBeneficiariesAsync();
                    _suggestionsRequested = true;
                    ApplyFilter();
                    return;
                }

                // Remove automatic CRS master list sync/loading on view load to load local records instantly.
                // await AutoSyncCrsMasterListAsync();

                using var db = eSureHiDbContextFactory.Create();
                // AsNoTracking: this context is disposed at the end of the method and
                // every write path opens its own, so change tracking here buys nothing
                // and costs about a third of the load on a full CRS import.
                var records = await db.BeneficiaryStaging
                    .AsNoTracking()
                    .OrderBy(b => b.LastName)
                    .ThenBy(b => b.FirstName)
                    .ToListAsync();

                await EnrichWithDemographicsAsync(db, records);

                _allRecords.Clear();
                foreach (var r in records) _allRecords.Add(r);

                TotalCount = _allRecords.Count;
                UnlinkedCount = _allRecords.Count(r => r.LinkStatus == "Unlinked");
                LinkedCount = _allRecords.Count(r => r.LinkStatus == "Linked");
                await LoadSystemBeneficiariesAsync();
                _suggestionsRequested = true;
                ApplyFilter();
                
                if (TotalCount == 0 && !IsImporting)
                {
                    _ = StartImportAsync();
                }
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
        }

        // Repopulates the [NotMapped] demographic fields (position/family role,
        // family id, head-of-family flag) on staging rows from the persisted
        // crs_beneficiary_cache. Without this, rows reloaded from beneficiary_staging
        // lose the position that was only held in memory during import, so the
        // role badge falls through to "UNCLASSIFIED".
        /// <summary>
        /// Batch size up to which the demographics read is narrowed to the ids being
        /// enriched rather than sweeping the whole cache table.
        /// </summary>
        private const int ScopedEnrichmentLimit = 500;

        private static async Task EnrichWithDemographicsAsync(
            eSureHiDbContext db,
            System.Collections.Generic.IReadOnlyList<BeneficiaryStaging> records,
            CancellationToken cancellationToken = default)
        {
            if (records.Count == 0)
                return;

            var cacheQuery = db.CrsBeneficiaryCache.AsNoTracking();

            // Reading the whole cache to decorate a handful of rows is the dominant cost
            // on the search path — 40k rows fetched to enrich 80. Scope the read to the
            // ids actually being enriched when that fits an IN-list; a whole-table load
            // wants every row anyway, so it keeps the single sweep.
            if (records.Count <= ScopedEnrichmentLimit)
            {
                var beneficiaryIds = records
                    .Where(r => !string.IsNullOrWhiteSpace(r.BeneficiaryId))
                    .Select(r => r.BeneficiaryId!)
                    .Distinct()
                    .ToList();

                var residentIds = records
                    .Where(r => r.ResidentsId.HasValue)
                    .Select(r => r.ResidentsId!.Value)
                    .Distinct()
                    .ToList();

                cacheQuery = cacheQuery.Where(c =>
                    (c.BeneficiaryId != null && beneficiaryIds.Contains(c.BeneficiaryId)) ||
                    (c.ResidentsId.HasValue && residentIds.Contains(c.ResidentsId.Value)));
            }

            var cacheRows = await cacheQuery.ToListAsync(cancellationToken);

            if (cacheRows.Count == 0)
                return;

            var byBeneficiaryId = new System.Collections.Generic.Dictionary<string, CrsBeneficiaryCache>(
                StringComparer.OrdinalIgnoreCase);
            var byResidentsId = new System.Collections.Generic.Dictionary<long, CrsBeneficiaryCache>();

            foreach (var c in cacheRows)
            {
                if (!string.IsNullOrWhiteSpace(c.BeneficiaryId))
                    byBeneficiaryId[c.BeneficiaryId] = c;
                if (c.ResidentsId.HasValue)
                    byResidentsId[c.ResidentsId.Value] = c;
            }

            foreach (var record in records)
            {
                CrsBeneficiaryCache? match = null;
                if (!string.IsNullOrWhiteSpace(record.BeneficiaryId))
                    byBeneficiaryId.TryGetValue(record.BeneficiaryId, out match);
                if (match is null && record.ResidentsId.HasValue)
                    byResidentsId.TryGetValue(record.ResidentsId.Value, out match);

                if (match is null)
                    continue;

                record.FamilyRole = match.FamilyRole;
                record.DemographicFamilyRole = match.FamilyRole ?? string.Empty;
                record.DemographicFamilyId = match.FamilyId ?? string.Empty;
                record.DemographicRelationshipToHead = match.RelationshipToHead ?? string.Empty;
                record.IsDemographicHeadOfFamily = match.IsHouseholdHead;
                record.HasDemographicProfile = !string.IsNullOrWhiteSpace(match.FamilyRole);
            }
        }

        /// <summary>
        /// Debounced entry point for keystroke-driven filtering. ApplyFilter used to run
        /// straight off the SearchText setter, once per keystroke, over the whole
        /// master list — on a 40k-row import that is roughly a second of frozen UI per
        /// character. Same 180ms window the selection-only path already uses, so a burst
        /// of typing costs one pass instead of one per key.
        /// </summary>
        private void QueueFilter()
        {
            _filterCts?.Cancel();
            var cts = new CancellationTokenSource();
            _filterCts = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(FilterDebounceMs, cts.Token);
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!cts.IsCancellationRequested)
                            ApplyFilter();
                    });
                }
                catch (TaskCanceledException)
                {
                }
            });
        }

        private void QueueSelectionSearch()
        {
            _selectionSearchCts?.Cancel();
            var cts = new CancellationTokenSource();
            _selectionSearchCts = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(180, cts.Token);
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (!cts.IsCancellationRequested)
                            await LoadSelectionRecordsAsync(cts.Token);
                    });
                }
                catch (TaskCanceledException)
                {
                }
            });
        }

        private async Task LoadSelectionRecordsAsync(CancellationToken cancellationToken = default)
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var search = SearchText.Trim();
                var searchLower = search.ToLower();

                if (IsCrsSource)
                {
                    var query = db.BeneficiaryStaging.AsNoTracking();

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        query = query.Where(r =>
                            (r.FullName != null && r.FullName.ToLower().Contains(searchLower)) ||
                            (r.LastName != null && r.LastName.ToLower().Contains(searchLower)) ||
                            (r.FirstName != null && r.FirstName.ToLower().Contains(searchLower)) ||
                            (r.MiddleName != null && r.MiddleName.ToLower().Contains(searchLower)) ||
                            (r.BeneficiaryId != null && r.BeneficiaryId.ToLower().Contains(searchLower)) ||
                            (r.CivilRegistryId != null && r.CivilRegistryId.ToLower().Contains(searchLower)) ||
                            (r.ResidentsId.HasValue && r.ResidentsId.Value.ToString().Contains(searchLower)));
                    }

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var records = await query
                        .OrderBy(r => r.LastName)
                        .ThenBy(r => r.FirstName)
                        .Take(80)
                        .ToListAsync(cancellationToken);
                    sw.Stop();
                    System.Diagnostics.Debug.WriteLine($"[LoadSelectionRecordsAsync - CRS] DB Query took {sw.ElapsedMilliseconds} ms for SearchText='{search}'");

                    await EnrichWithDemographicsAsync(db, records, cancellationToken);

                    DisplayedRecords.Clear();
                    foreach (var record in records)
                        DisplayedRecords.Add(record);

                    TotalCount = records.Count;
                    FilteredCount = records.Count;
                    UnlinkedCount = records.Count(r => r.LinkStatus == "Unlinked");
                    LinkedCount = records.Count(r => r.LinkStatus == "Linked");
                }
                else
                {
                    var query = db.Beneficiaries.AsNoTracking().Include(b => b.Employee).Where(b => b.IsActive);

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        query = query.Where(b =>
                            (b.FullName != null && b.FullName.ToLower().Contains(searchLower)) ||
                            (b.Relationship != null && b.Relationship.ToLower().Contains(searchLower)) ||
                            (b.Employee != null && b.Employee.FullName != null && b.Employee.FullName.ToLower().Contains(searchLower)) ||
                            (b.Employee != null && b.Employee.EmployeeNo != null && b.Employee.EmployeeNo.ToLower().Contains(searchLower)) ||
                            (b.BeneficiaryId != null && b.BeneficiaryId.ToLower().Contains(searchLower)));
                    }

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var list = await query
                        .OrderBy(b => b.LastName)
                        .ThenBy(b => b.FirstName)
                        .Take(80)
                        .ToListAsync(cancellationToken);
                    sw.Stop();
                    System.Diagnostics.Debug.WriteLine($"[LoadSelectionRecordsAsync - System] DB Query took {sw.ElapsedMilliseconds} ms for SearchText='{search}'");

                    DisplayedSystemBeneficiaries.Clear();
                    foreach (var beneficiary in list)
                        DisplayedSystemBeneficiaries.Add(beneficiary);

                    SystemTotalCount = list.Count;
                    SystemFilteredCount = list.Count;
                }

                _suggestionsRequested = true;
                UpdateSuggestions();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load CRS list failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilter()
        {
            var q = _allRecords.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var s = SearchText.Trim().ToLower();
                q = q.Where(r =>
                    (r.FullName?.ToLower().Contains(s) ?? false) ||
                    (r.BeneficiaryId?.ToLower().Contains(s) ?? false) ||
                    (r.LastName?.ToLower().Contains(s) ?? false) ||
                    (r.FirstName?.ToLower().Contains(s) ?? false));
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter) && StatusFilter != "All")
            {
                q = q.Where(r => r.LinkStatus == StatusFilter);
            }

            var matches = q.ToList();

            DisplayedRecords.Clear();
            foreach (var r in matches.Take(MaxDisplayedRows)) DisplayedRecords.Add(r);
            FilteredCount = matches.Count;

            var local = _allSystemBeneficiaries.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var s = SearchText.Trim().ToLower();
                local = local.Where(b =>
                    (b.FullName?.ToLower().Contains(s) ?? false) ||
                    (b.Relationship?.ToLower().Contains(s) ?? false) ||
                    (b.Employee?.FullName?.ToLower().Contains(s) ?? false) ||
                    (b.Employee?.EmployeeNo?.ToLower().Contains(s) ?? false));
            }

            var localMatches = local.ToList();

            DisplayedSystemBeneficiaries.Clear();
            foreach (var b in localMatches.Take(MaxDisplayedRows)) DisplayedSystemBeneficiaries.Add(b);
            SystemFilteredCount = localMatches.Count;

            UpdateSuggestions();
        }

        /// <summary>
        /// Mirrors the top of the active source's already-filtered list into the
        /// suggestion popup. Reads the displayed collections rather than re-querying,
        /// so the popup and the ListBox underneath can never disagree.
        /// </summary>
        private void UpdateSuggestions()
        {
            if (SelectionOnly)
            {
                IsSuggestionsOpen = false;
                return;
            }

            var requested = _suggestionsRequested;
            _suggestionsRequested = false;

            SearchSuggestions.Clear();

            // Nothing typed means nothing to suggest — otherwise the initial load, which
            // also asks for suggestions, pops the list open over an untouched search box.
            if (!requested || string.IsNullOrWhiteSpace(SearchText))
            {
                IsSuggestionsOpen = false;
                return;
            }

            var rows = IsCrsSource
                ? DisplayedRecords.Take(MaxSuggestions).Select(BeneficiarySuggestionRow.FromRecord)
                : DisplayedSystemBeneficiaries.Take(MaxSuggestions).Select(BeneficiarySuggestionRow.FromBeneficiary);

            foreach (var row in rows)
                SearchSuggestions.Add(row);

            IsSuggestionsOpen = SearchSuggestions.Count > 0;
        }

        /// <summary>
        /// Picking a suggestion does exactly what clicking the matching ListBox row
        /// does — it sets the same selection property, so the staging/edit flow,
        /// profile load and command CanExecute all run unchanged.
        /// </summary>
        private void ApplySuggestion(BeneficiarySuggestionRow? row)
        {
            if (row is null)
                return;

            IsSuggestionsOpen = false;

            if (row.Record is not null)
                SelectedRecord = row.Record;
            else if (row.Beneficiary is not null)
                SelectedSystemBeneficiary = row.Beneficiary;
        }

        // ── Load Employees ─────────────────────────────────────────────
        private async Task LoadEmployeesAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var list = await db.Employees
                    .Where(e => e.EmploymentStatus == "Active")
                    .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                    .ToListAsync();
                Employees.Clear();
                foreach (var e in list) Employees.Add(e);
                ApplyEmployeeFilter();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load employees failed: {ex.Message}";
            }
        }

        private async Task LoadInsurancePoliciesAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var policies = await db.InsurancePolicies
                    .Where(p => p.PolicyStatus == "Active")
                    .OrderBy(p => p.PolicyName)
                    .ToListAsync();

                InsurancePolicies.Clear();
                foreach (var policy in policies)
                    InsurancePolicies.Add(policy);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load insurance policies failed: {ex.Message}";
            }
        }

        private async Task LoadSystemBeneficiariesAsync()
        {
            using var db = eSureHiDbContextFactory.Create();
            var list = await db.Beneficiaries
                .Include(b => b.Employee)
                .Where(b => b.IsActive)
                .OrderBy(b => b.LastName)
                .ThenBy(b => b.FirstName)
                .ToListAsync();

            _allSystemBeneficiaries.Clear();
            foreach (var beneficiary in list)
                _allSystemBeneficiaries.Add(beneficiary);

            SystemTotalCount = _allSystemBeneficiaries.Count;
        }

        private static string MapRoleToRelationship(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return "Other";

            var mapped = BeneficiaryStaging.MapPositionToLabel(role);
            if (string.IsNullOrWhiteSpace(mapped))
                return "Other";

            return mapped.ToUpperInvariant() switch
            {
                "SPOUSE" => "Spouse",
                "SON" => "Son",
                "DAUGHTER" => "Daughter",
                "CHILD" or "CHILDREN" => "Child",
                "FATHER" or "MOTHER" or "PARENT" => "Parent",
                "BROTHER" or "SISTER" or "SIBLING" => "Sibling",
                _ => "Other"
            };
        }

        private async Task OpenFamilyMemberProfileAsync(FamilyMemberDisplay familyMember)
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                
                var stagingRecord = await db.BeneficiaryStaging
                    .FirstOrDefaultAsync(b => b.CivilRegistryId == familyMember.CivilRegistryId || 
                                              (!string.IsNullOrEmpty(familyMember.BeneficiaryId) && b.BeneficiaryId == familyMember.BeneficiaryId));

                if (stagingRecord == null)
                {
                    var crsRecord = await db.CrsBeneficiaryCache
                        .FirstOrDefaultAsync(c => c.CivilRegistryId == familyMember.CivilRegistryId || 
                                                  (!string.IsNullOrEmpty(familyMember.BeneficiaryId) && c.BeneficiaryId == familyMember.BeneficiaryId));

                    if (crsRecord != null)
                    {
                        stagingRecord = new BeneficiaryStaging
                        {
                            BeneficiaryId = crsRecord.BeneficiaryId,
                            CivilRegistryId = crsRecord.CivilRegistryId,
                            FirstName = crsRecord.FirstName,
                            LastName = crsRecord.LastName,
                            Sex = crsRecord.Sex,
                            DateOfBirth = crsRecord.DateOfBirth,
                            Address = crsRecord.Address,
                            DemographicFamilyId = crsRecord.FamilyId ?? string.Empty,
                            DemographicFamilyRole = crsRecord.FamilyRole ?? string.Empty,
                            LinkStatus = "Unlinked",
                            CedulaNo = crsRecord.CedulaNo
                        };
                    }
                }

                if (stagingRecord != null)
                {
                    SelectedRecord = stagingRecord;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to open family member profile: {ex.Message}";
            }
            finally
            {
                _selectedFamilyMember = null;
                OnPropertyChanged(nameof(SelectedFamilyMember));
            }
        }

        private async Task OpenProfileAsync(BeneficiaryStaging record)
        {
            if (record is null)
                return;

            _isLoadingProfile = true;
            IsLoading = true;
            ErrorMessage = string.Empty;
            ResetLinkFormFields();
            ProfileBenefits.Clear();
            ProfileTransactions.Clear();
            ProfileCedulas.Clear();
            ProfileRequirements.Clear();
            FamilyMembers.Clear();
            ProfileBeneficiary = null;
            ProfileEmployee = null;
            ProfileAddress = record.Address ?? string.Empty;
            ProfileStatus = record.LinkStatus == "Linked" ? "Already in Insurance System" : "CRS master list record";
            ProfileCedulaStatus = "No cedula payment found";
            ProfileTransactionStatus = "Searching transactions...";
            EligibilityStatus = "Evaluating...";
            GgmsSyncStatus.Clear();

            try
            {
                using var db = eSureHiDbContextFactory.Create();

                if (string.IsNullOrWhiteSpace(record.DemographicFamilyId))
                {
                    CrsBeneficiaryCache? cacheMatch = null;
                    if (!string.IsNullOrWhiteSpace(record.BeneficiaryId))
                    {
                        cacheMatch = await db.CrsBeneficiaryCache
                            .AsNoTracking()
                            .FirstOrDefaultAsync(c => c.BeneficiaryId == record.BeneficiaryId);
                    }
                    if (cacheMatch is null && !string.IsNullOrWhiteSpace(record.CivilRegistryId))
                    {
                        cacheMatch = await db.CrsBeneficiaryCache
                            .AsNoTracking()
                            .FirstOrDefaultAsync(c => c.CivilRegistryId == record.CivilRegistryId);
                    }
                    if (cacheMatch is not null)
                    {
                        record.FamilyRole = cacheMatch.FamilyRole;
                        record.DemographicFamilyRole = cacheMatch.FamilyRole ?? string.Empty;
                        record.DemographicFamilyId = cacheMatch.FamilyId ?? string.Empty;
                        record.DemographicRelationshipToHead = cacheMatch.RelationshipToHead ?? string.Empty;
                        record.IsDemographicHeadOfFamily = cacheMatch.IsHouseholdHead;
                        record.HasDemographicProfile = !string.IsNullOrWhiteSpace(cacheMatch.FamilyRole);
                    }
                }

                if (record.LinkedBenId.HasValue)
                {
                    ProfileBeneficiary = await db.Beneficiaries
                        .Include(b => b.Employee)
                        .FirstOrDefaultAsync(b => b.BenId == record.LinkedBenId.Value);
                    ProfileEmployee = ProfileBeneficiary?.Employee;

                    if (ProfileBeneficiary is not null &&
                        SelectedSystemBeneficiary?.BenId != ProfileBeneficiary.BenId)
                    {
                        _isSyncingSystemBeneficiarySelection = true;
                        try
                        {
                            _selectedSystemBeneficiary = ProfileBeneficiary;
                            OnPropertyChanged(nameof(SelectedSystemBeneficiary));
                            RefreshCanExecute();
                            OnPropertyChanged(nameof(CanRemoveBeneficiary));
                        }
                        finally
                        {
                            _isSyncingSystemBeneficiarySelection = false;
                        }
                    }
                    
                    if (ProfileBeneficiary != null)
                    {
                        Relationship = string.IsNullOrWhiteSpace(ProfileBeneficiary.Relationship)
                            ? "Other"
                            : ProfileBeneficiary.Relationship;
                        IsPrimary = ProfileBeneficiary.IsPrimary;
                        RecipientsInsurance = ProfileBeneficiary.RecipientsInsurance ?? string.Empty;
                        CedulaNo = ProfileBeneficiary.CedulaNo ?? string.Empty;
                        Received = ProfileBeneficiary.Received;
                        Contribution = ProfileBeneficiary.Contribution;
                        SourceOfFunds = string.IsNullOrWhiteSpace(ProfileBeneficiary.SourceOfFunds)
                            ? SourceOfFundsOptions.FirstOrDefault() ?? "Job Order"
                            : ProfileBeneficiary.SourceOfFunds;
                        SelectedPolicy = InsurancePolicies.FirstOrDefault(p => p.PolicyName == ProfileBeneficiary.RecipientsInsurance);
                    }
                }
                else if (record.LinkedEmpId.HasValue)
                {
                    ProfileEmployee = await db.Employees
                        .FirstOrDefaultAsync(e => e.EmpId == record.LinkedEmpId.Value);
                }

                if (ProfileBeneficiary == null)
                {
                    // Autofill for fresh CRS/unlinked record
                    var role = record.DemographicFamilyRole;
                    Relationship = MapRoleToRelationship(role);
                    IsPrimary = record.IsDemographicHeadOfFamily || 
                                (!string.IsNullOrEmpty(role) && role.Equals("Head of Family", StringComparison.OrdinalIgnoreCase));
                    SourceOfFunds = SourceOfFundsOptions.FirstOrDefault() ?? "Job Order";
                    Contribution = GetDefaultMonthlyContribution(SourceOfFunds);
                    CedulaNo = record.CedulaNo ?? string.Empty;
                }

                if (ProfileEmployee is not null)
                {
                    ProfileAddress = string.Join(", ", new[]
                    {
                        ProfileEmployee.AddressLine1,
                        ProfileEmployee.Barangay,
                        ProfileEmployee.City,
                        ProfileEmployee.Province
                    }.Where(part => !string.IsNullOrWhiteSpace(part)));

                    var cedulas = await db.Cedulas
                        .Where(c => c.EmployeeId == ProfileEmployee.EmpId)
                        .OrderByDescending(c => c.IssueDate)
                        .ToListAsync();
                    foreach (var c in cedulas) ProfileCedulas.Add(c);
                    ProfileCedulaStatus = cedulas.Any(c => c.AmountPaid > 0)
                        ? "Cedula paid"
                        : "No cedula payment found";

                    var benefits = await db.Benefits
                        .Include(b => b.EmployeePolicy)
                            .ThenInclude(ep => ep!.Policy)
                        .Where(b => b.EmployeePolicy != null &&
                                    b.EmployeePolicy.EmpId == ProfileEmployee.EmpId)
                        .OrderByDescending(b => b.YearPeriod)
                        .ThenBy(b => b.BenefitType)
                        .ToListAsync();
                    foreach (var b in benefits) ProfileBenefits.Add(b);

                    ProfilePremiums.Clear();
                    var premiums = await db.Premiums
                        .Include(p => p.EmployeePolicy)
                            .ThenInclude(ep => ep!.Policy)
                        .Where(p => p.EmployeePolicy != null &&
                                    p.EmployeePolicy.EmpId == ProfileEmployee.EmpId)
                        .OrderByDescending(p => p.PaidDate ?? p.DueDate)
                        .ToListAsync();
                    foreach (var p in premiums) ProfilePremiums.Add(p);

                    var documentIds = await db.Documents
                        .Where(d => d.EmpId == ProfileEmployee.EmpId)
                        .Select(d => d.DocumentId)
                        .ToListAsync();
                    var transactions = await db.DocumentTransactions
                        .Include(t => t.Sender)
                        .Include(t => t.Receiver)
                        .Where(t => t.DocumentId.HasValue &&
                                    documentIds.Contains(t.DocumentId.Value))
                        .OrderByDescending(t => t.CreatedAt)
                        .ToListAsync();
                    foreach (var t in transactions) ProfileTransactions.Add(t);
                }

                // ── Fetch from GGMS Database ───────────────────────────
                try
                {
                    using var ggmsDb = GgmsDbContextFactory.Create();
                    var ggmsTransactions = await ggmsDb.GgmsTransactions
                        .Where(t => t.BeneficiaryId == record.BeneficiaryId ||
                                   (record.CivilRegistryId != null && t.CivilRegistryId == record.CivilRegistryId))
                        .OrderByDescending(t => t.TransactionDate)
                        .ToListAsync();

                    foreach (var gt in ggmsTransactions)
                    {
                        ProfileTransactions.Add(new DocumentTransaction
                        {
                            TransactionNo = gt.ProjectCode,
                            Subject = $"{gt.ProjectName} ({gt.OfficeName})",
                            TransactionType = gt.TransactionType,
                            Status = gt.Status,
                            TransactionDate = gt.TransactionDate
                        });
                    }

                    // Check for Benefit releases in GGMS
                    var ggmsBenefits = ggmsTransactions.Where(t => t.TransactionType.Contains("Benefit")).ToList();
                    foreach (var gb in ggmsBenefits)
                    {
                        ProfileBenefits.Add(new Benefit
                        {
                            BenefitType = gb.TransactionType,
                            UsedBenefit = gb.Amount,
                            YearPeriod = gb.TransactionDate.Year
                        });
                    }
                }
                catch
                {
                    GgmsSyncStatus.Set("GGMS unavailable — showing local transactions and benefits only.", StatusSeverity.Warning);
                }

                ProfileTransactionStatus = ProfileTransactions.Any()
                    ? $"{ProfileTransactions.Count} transaction(s) found"
                    : "No transactions found";

                if (!string.IsNullOrWhiteSpace(record.DemographicFamilyId))
                {
                    var familyMembers = await db.CrsBeneficiaryCache
                        .Where(c => c.FamilyId == record.DemographicFamilyId)
                        .ToListAsync();

                    var civilRegistryIds = familyMembers
                        .Where(m => !string.IsNullOrWhiteSpace(m.CivilRegistryId))
                        .Select(m => m.CivilRegistryId)
                        .ToList();

                    var activeBeneficiaries = await db.Beneficiaries
                        .Where(b => b.IsActive && civilRegistryIds.Contains(b.CivilRegistryId))
                        .Select(b => new { b.CivilRegistryId, b.IsPrimary })
                        .ToListAsync();

                    foreach (var member in familyMembers.OrderBy(m => m.IsHouseholdHead ? 0 : 1).ThenBy(m => m.FullName))
                    {
                        var matchesCivilRegistryId = !string.IsNullOrWhiteSpace(member.CivilRegistryId) && 
                                                    !string.IsNullOrWhiteSpace(record.CivilRegistryId) && 
                                                    member.CivilRegistryId == record.CivilRegistryId;

                        var matchesBeneficiaryId = !string.IsNullOrWhiteSpace(member.BeneficiaryId) && 
                                                   !string.IsNullOrWhiteSpace(record.BeneficiaryId) && 
                                                   member.BeneficiaryId == record.BeneficiaryId;

                        if (matchesCivilRegistryId || matchesBeneficiaryId)
                            continue;

                        var status = "Not Registered";
                        var bg = "#F1F5F9";
                        var fg = "#64748B";

                        if (!string.IsNullOrWhiteSpace(member.CivilRegistryId))
                        {
                            var ben = activeBeneficiaries.FirstOrDefault(b => b.CivilRegistryId == member.CivilRegistryId);
                            if (ben != null)
                            {
                                if (ben.IsPrimary)
                                {
                                    status = "Registered Member";
                                    bg = "#DCFCE7";
                                    fg = "#166534";
                                }
                                else
                                {
                                    status = "Dependent";
                                    bg = "#DBEAFE";
                                    fg = "#1E40AF";
                                }
                            }
                        }

                        FamilyMembers.Add(new FamilyMemberDisplay
                        {
                            FullName = member.FullName ?? "Unknown",
                            FamilyRole = string.IsNullOrWhiteSpace(member.FamilyRole) ? "MEMBER" : member.FamilyRole.ToUpper(),
                            RegistrationStatus = status,
                            RegistrationStatusBackground = bg,
                            RegistrationStatusForeground = fg,
                            CivilRegistryId = member.CivilRegistryId ?? string.Empty,
                            BeneficiaryId = member.BeneficiaryId ?? string.Empty
                        });
                    }
                }

                BuildRequirements(record);
                OnPropertyChanged(nameof(IsProfileOpen));
                OnPropertyChanged(nameof(ShowRegistrationButtons));
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load beneficiary profile failed: {ex.Message}";
            }
            finally 
            { 
                _isLoadingProfile = false;
                IsLoading = false; 
            }
        }

        private void ResetLinkFormFields()
        {
            SelectedPolicy = null;
            Relationship = "Other";
            IsPrimary = false;
            RecipientsInsurance = string.Empty;
            CedulaNo = string.Empty;
            Received = false;
            Contribution = 0;
            SourceOfFunds = SourceOfFundsOptions.FirstOrDefault() ?? "Job Order";
            SelectedEmployee = null;
            EmployeeSearch = string.Empty;
        }

        private async Task AutoSyncCrsMasterListAsync(bool force = false)
        {
            if ((_hasAutoSyncedCrs && !force) || IsImporting)
                return;

            var crsConfig = Data.SharedDatabaseConfiguration.LoadCrs();
            if (!crsConfig.IsConfigured)
                return;

            _hasAutoSyncedCrs = true;
            IsImporting = true;
            ImportProgress = 0;
            ImportTotal = 0;
            ImportMessage = "Loading CRS master list...";

            var progressHandler = new Progress<CrsImportProgress>(p =>
            {
                ImportProgress = p.Imported;
                ImportTotal = p.Total > 0 ? p.Total : ImportTotal;
                ImportMessage = p.Message;
                OnPropertyChanged(nameof(ImportPercent));
            });

            try
            {
                await CrsImportService.ImportAsync(
                    crsConfig.ToConnectionString(),
                    progressHandler);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"CRS master list sync failed: {ex.Message}";
            }
            finally
            {
                IsImporting = false;
            }
        }

        private void CloseProfile()
        {
            _selectedRecord = null;
            _selectedSystemBeneficiary = null;
            OnPropertyChanged(nameof(SelectedRecord));
            OnPropertyChanged(nameof(SelectedSystemBeneficiary));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(IsProfileOpen));
            ProfileBenefits.Clear();
            ProfileTransactions.Clear();
            ProfileCedulas.Clear();
            ProfileRequirements.Clear();
            ProfileBeneficiary = null;
            ProfileEmployee = null;
            EligibilityStatus = "Reviewing...";
            QualificationReason = "Select a beneficiary to review qualification.";
            OnPropertyChanged(nameof(IsQualifiedForInsurance));
            OnPropertyChanged(nameof(CanAddBeneficiary));
            OnPropertyChanged(nameof(CanRemoveBeneficiary));
            LinkCommand.RaiseCanExecuteChanged();
            SkipCommand.RaiseCanExecuteChanged();
            RemoveBeneficiaryCommand.RaiseCanExecuteChanged();
        }

        private void BuildRequirements(BeneficiaryStaging record)
        {
            ProfileRequirements.Clear();

            var hasName = !string.IsNullOrWhiteSpace(record.FirstName) &&
                          !string.IsNullOrWhiteSpace(record.LastName);
            var hasIdentity = !string.IsNullOrWhiteSpace(record.BeneficiaryId) ||
                              !string.IsNullOrWhiteSpace(record.CivilRegistryId);
            var hasBirthDate = !string.IsNullOrWhiteSpace(record.DateOfBirth);
            var hasSex = !string.IsNullOrWhiteSpace(record.Sex);
            var isUnlinked = record.LinkStatus == "Unlinked";

            ProfileRequirements.Add(new RequirementStatus("Validated in CRS master list", hasIdentity));
            ProfileRequirements.Add(new RequirementStatus("Complete beneficiary name", hasName));
            ProfileRequirements.Add(new RequirementStatus("Birth date on record", hasBirthDate, false));
            ProfileRequirements.Add(new RequirementStatus("Sex / gender on record", hasSex, false));
            ProfileRequirements.Add(new RequirementStatus("Employee link (Optional)", ProfileEmployee is not null || SelectedEmployee is not null, false));
            ProfileRequirements.Add(new RequirementStatus("Not yet already linked", isUnlinked));

            var requiredReady = isUnlinked && !string.IsNullOrWhiteSpace(record.DisplayName);

            EligibilityStatus = requiredReady
                ? "Ready to add to insurance records"
                : "Enter a beneficiary name to add";
            
            if (isUnlinked)
            {
                QualificationReason = requiredReady
                    ? "Ready: This CRS record can be added now. Qualifications and employee link are not required for beta testing."
                    : "Enter at least one beneficiary name, then approve.";
            }
            else
            {
                QualificationReason = record.LinkStatus == "Linked"
                    ? "This record is already added to insurance records."
                    : "This CRS record can still be added now. Qualifications and employee link are not required for beta testing.";
            }

            RefreshQualificationState();
        }

        private async Task LoadSelectedEmployeeContextAsync()
        {
            if (SelectedEmployee is null || SelectedRecord is null)
            {
                if (SelectedRecord is not null)
                    BuildRequirements(SelectedRecord);
                return;
            }

            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                ProfileEmployee = await db.Employees
                    .FirstOrDefaultAsync(e => e.EmpId == SelectedEmployee.EmpId);

                if (ProfileEmployee is not null)
                {
                    ProfileAddress = string.Join(", ", new[]
                    {
                        ProfileEmployee.AddressLine1,
                        ProfileEmployee.Barangay,
                        ProfileEmployee.City,
                        ProfileEmployee.Province
                    }.Where(part => !string.IsNullOrWhiteSpace(part)));

                    ProfileCedulas.Clear();
                    var cedulas = await db.Cedulas
                        .Where(c => c.EmployeeId == ProfileEmployee.EmpId)
                        .OrderByDescending(c => c.IssueDate)
                        .ToListAsync();
                    foreach (var c in cedulas) ProfileCedulas.Add(c);
                    ProfileCedulaStatus = cedulas.Any(c => c.AmountPaid > 0)
                        ? "Cedula paid"
                        : "No cedula payment found";

                    ProfileBenefits.Clear();
                    var benefits = await db.Benefits
                        .Include(b => b.EmployeePolicy)
                            .ThenInclude(ep => ep!.Policy)
                        .Where(b => b.EmployeePolicy != null &&
                                    b.EmployeePolicy.EmpId == ProfileEmployee.EmpId)
                        .OrderByDescending(b => b.YearPeriod)
                        .ThenBy(b => b.BenefitType)
                        .ToListAsync();
                    foreach (var b in benefits) ProfileBenefits.Add(b);

                    ProfilePremiums.Clear();
                    var premiums = await db.Premiums
                        .Include(p => p.EmployeePolicy)
                            .ThenInclude(ep => ep!.Policy)
                        .Where(p => p.EmployeePolicy != null &&
                                    p.EmployeePolicy.EmpId == ProfileEmployee.EmpId)
                        .OrderByDescending(p => p.PaidDate ?? p.DueDate)
                        .ToListAsync();
                    foreach (var p in premiums) ProfilePremiums.Add(p);

                    ProfileTransactions.Clear();
                    var documentIds = await db.Documents
                        .Where(d => d.EmpId == ProfileEmployee.EmpId)
                        .Select(d => d.DocumentId)
                        .ToListAsync();
                    var transactions = await db.DocumentTransactions
                        .Include(t => t.Sender)
                        .Include(t => t.Receiver)
                        .Where(t => t.DocumentId.HasValue &&
                                    documentIds.Contains(t.DocumentId.Value))
                        .OrderByDescending(t => t.CreatedAt)
                        .ToListAsync();
                    foreach (var t in transactions) ProfileTransactions.Add(t);
                }

                BuildRequirements(SelectedRecord);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load employee context failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        private void RefreshQualificationState()
        {
            OnPropertyChanged(nameof(IsQualifiedForInsurance));
            OnPropertyChanged(nameof(CanAddBeneficiary));
            OnPropertyChanged(nameof(CanRemoveBeneficiary));
            LinkCommand.RaiseCanExecuteChanged();
            RemoveBeneficiaryCommand.RaiseCanExecuteChanged();
        }

        private int? GetSelectedInsuranceBeneficiaryId()
        {
            if (SelectedSystemBeneficiary is not null)
                return SelectedSystemBeneficiary.BenId;

            if (ProfileBeneficiary is not null)
                return ProfileBeneficiary.BenId;

            return SelectedRecord?.LinkedBenId;
        }

        private async Task RemoveBeneficiaryAsync()
        {
            var beneficiaryId = GetSelectedInsuranceBeneficiaryId();
            if (beneficiaryId is null)
                return;

            var displayName = SelectedSystemBeneficiary?.FullName
                ?? ProfileBeneficiary?.FullName
                ?? SelectedRecord?.DisplayName
                ?? "this beneficiary";

            var result = MessageBox.Show(
                $"Remove {displayName} from insurance beneficiaries?\n\n" +
                "The CRS/master-list record will stay. If it came from CRS, it can be added again later.",
                "Remove Insurance Beneficiary",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            IsLoading = true;
            ErrorMessage = string.Empty;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var beneficiary = await db.Beneficiaries
                    .FirstOrDefaultAsync(b => b.BenId == beneficiaryId.Value);

                if (beneficiary is null)
                {
                    ErrorMessage = "Beneficiary record was not found.";
                    return;
                }

                beneficiary.IsActive = false;
                beneficiary.WorkflowStatus = WorkflowStatuses.Archived;
                beneficiary.StatusRemarks = "Removed from insurance beneficiaries.";

                var stagingRecords = await db.BeneficiaryStaging
                    .Where(s => s.LinkedBenId == beneficiary.BenId)
                    .ToListAsync();

                foreach (var staging in stagingRecords)
                {
                    staging.LinkStatus = "Unlinked";
                    staging.LinkedBenId = null;
                    staging.LinkedEmpId = null;
                }

                await db.SaveChangesAsync();

                ErrorMessage = "Beneficiary removed from insurance records.";
                CloseProfile();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Remove beneficiary failed: {ex.GetBaseException().Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Link ───────────────────────────────────────────────────────
        private bool EnsureEmployeeSelectedForApproval()
        {
            if (SelectedEmployee is not null)
                return true;

            if (FilteredEmployees.Count == 1)
            {
                SelectedEmployee = FilteredEmployees[0];
                return true;
            }

            ErrorMessage = "Please select the employee this beneficiary belongs to before approving.";
            MessageBox.Show(
                "Please select an employee from the list first, then click Approve Beneficiary again.",
                "Employee Link Required",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        private static async Task<Employee> GetOrCreateUnlinkedBeneficiaryEmployeeAsync(eSureHiDbContext db, DateOnly? beneficiaryDateOfBirth)
        {
            const string placeholderEmployeeNo = "UNLINKED-BENEFICIARY";
            var fallbackDateOfBirth = beneficiaryDateOfBirth ?? DateOnly.FromDateTime(DateTime.Today.AddYears(-18));

            var employee = await db.Employees
                .FirstOrDefaultAsync(e => e.EmployeeNo == placeholderEmployeeNo);
            if (employee is not null)
            {
                if (employee.DateOfBirth is null)
                {
                    employee.DateOfBirth = fallbackDateOfBirth;
                    employee.UpdatedAt = DateTime.Now;
                    await db.SaveChangesAsync();
                }
                return employee;
            }

            employee = new Employee
            {
                EmployeeNo = placeholderEmployeeNo,
                FirstName = "Unlinked",
                LastName = "Beneficiary",
                DateOfBirth = fallbackDateOfBirth,
                Gender = "Other",
                CivilStatus = "Single",
                Nationality = "Filipino",
                Email = "unlinked-beneficiary@local.invalid",
                EmploymentType = "Beneficiary",
                EmploymentStatus = "Inactive",
                DateHired = DateOnly.FromDateTime(DateTime.Today),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            db.Employees.Add(employee);
            await db.SaveChangesAsync();
            return employee;
        }

        private static void NormalizeBeneficiaryName(BeneficiaryStaging record)
        {
            record.FirstName = record.FirstName?.Trim();
            record.LastName = record.LastName?.Trim();
            record.FullName = record.FullName?.Trim();

            if (string.IsNullOrWhiteSpace(record.FirstName) && string.IsNullOrWhiteSpace(record.LastName))
            {
                var displayName = record.DisplayName?.Trim();
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    if (displayName.Contains(','))
                    {
                        var parts = displayName.Split(',', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        record.LastName = parts.Length > 0 ? parts[0] : "N/A";
                        record.FirstName = parts.Length > 1 ? parts[1] : record.LastName;
                    }
                    else
                    {
                        var parts = displayName.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        record.FirstName = parts.Length > 0 ? parts[0] : displayName;
                        record.LastName = parts.Length > 1 ? parts[1] : "N/A";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(record.FirstName) && !string.IsNullOrWhiteSpace(record.LastName))
                record.FirstName = record.LastName;
            if (!string.IsNullOrWhiteSpace(record.FirstName) && string.IsNullOrWhiteSpace(record.LastName))
                record.LastName = "N/A";

            record.FullName = $"{record.FirstName} {record.LastName}".Trim();
        }

        public async Task<bool> AddSelectedRecordToInsuranceAsync(bool showMessage = true)
        {
            if (SelectedRecord is null) return false;
            var selectedEmployee = SelectedEmployee;

            NormalizeBeneficiaryName(SelectedRecord);
            if (string.IsNullOrWhiteSpace(SelectedRecord.FirstName) ||
                string.IsNullOrWhiteSpace(SelectedRecord.LastName))
            {
                ErrorMessage = "Please enter at least one beneficiary name before approving.";
                MessageBox.Show(
                    "Please enter at least one beneficiary name before approving.",
                    "Beneficiary Name Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return false;
            }

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var initialStatus = GetInitialMemberWorkflowStatus();
                var initialRemarks = GetInitialMemberStatusRemarks(initialStatus);

                // Parse date of birth safely
                var dob = ParseDateOnlyOrNull(SelectedRecord.DateOfBirth);
                if (dob.HasValue)
                    SelectedRecord.DateOfBirth = dob.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                var employeeForSave = selectedEmployee ?? await GetOrCreateUnlinkedBeneficiaryEmployeeAsync(db, dob);

                // Map sex to gender enum
                string gender = SelectedRecord.Sex?.ToUpper() switch
                {
                    "MALE" or "M" => "Male",
                    "FEMALE" or "F" => "Female",
                    _ => "Other"
                };

                var beneficiaryId = SelectedRecord.BeneficiaryId;
                var civilRegistryId = SelectedRecord.CivilRegistryId;
                var firstName = SelectedRecord.FirstName ?? string.Empty;
                var lastName = SelectedRecord.LastName ?? string.Empty;

                var existingBeneficiary = await db.Beneficiaries
                    .Include(b => b.Employee)
                    .FirstOrDefaultAsync(b =>
                        b.IsActive &&
                        ((!string.IsNullOrWhiteSpace(beneficiaryId) && b.BeneficiaryId == beneficiaryId) ||
                         (!string.IsNullOrWhiteSpace(civilRegistryId) && b.CivilRegistryId == civilRegistryId)));

                Beneficiary beneficiary;
                if (existingBeneficiary is not null)
                {
                    beneficiary = existingBeneficiary;
                    beneficiary.EmpId = selectedEmployee?.EmpId ?? beneficiary.EmpId;
                    beneficiary.FirstName = firstName;
                    beneficiary.LastName = lastName;
                    beneficiary.Relationship = Relationship;
                    beneficiary.DateOfBirth = dob;
                    beneficiary.Gender = gender;
                    beneficiary.IsPrimary = IsPrimary;
                    beneficiary.RecipientsInsurance = string.IsNullOrWhiteSpace(RecipientsInsurance) ? beneficiary.RecipientsInsurance : RecipientsInsurance.Trim();
                    beneficiary.CedulaNo = string.IsNullOrWhiteSpace(CedulaNo) ? beneficiary.CedulaNo : CedulaNo.Trim();
                    beneficiary.Received = Received || beneficiary.Received;
                    beneficiary.Contribution = Contribution != 0 ? Contribution : beneficiary.Contribution;
                    beneficiary.SourceOfFunds = string.IsNullOrWhiteSpace(SourceOfFunds) ? beneficiary.SourceOfFunds : SourceOfFunds.Trim();
                    beneficiary.WorkflowStatus = initialStatus;
                    beneficiary.StatusRemarks = initialRemarks;
                    beneficiary.IsAdminConfirmed = true;
                }
                else
                {
                    beneficiary = new Beneficiary
                    {
                        EmpId = employeeForSave.EmpId,
                        BeneficiaryId = SelectedRecord.BeneficiaryId,
                        CivilRegistryId = SelectedRecord.CivilRegistryId,
                        FirstName = firstName,
                        LastName = lastName,
                        Relationship = Relationship,
                        DateOfBirth = dob,
                        Gender = gender,
                        IsPrimary = IsPrimary,
                        RecipientsInsurance = string.IsNullOrWhiteSpace(RecipientsInsurance) ? null : RecipientsInsurance.Trim(),
                        CedulaNo = string.IsNullOrWhiteSpace(CedulaNo) ? null : CedulaNo.Trim(),
                        Received = Received,
                        Contribution = Contribution,
                        SourceOfFunds = string.IsNullOrWhiteSpace(SourceOfFunds) ? null : SourceOfFunds.Trim(),
                        WorkflowStatus = initialStatus,
                        StatusRemarks = initialRemarks,
                        IsActive = true,
                        IsAdminConfirmed = false,
                        CreatedAt = DateTime.Now
                    };
                    db.Beneficiaries.Add(beneficiary);
                }

                await db.SaveChangesAsync();

                // Update staging record
                BeneficiaryStaging? staging = null;
                if (SelectedRecord.StagingId > 0)
                    staging = await db.BeneficiaryStaging.FindAsync(SelectedRecord.StagingId);

                if (staging is not null)
                {
                    staging.LinkStatus = "Linked";
                    staging.LinkedEmpId = beneficiary.EmpId;
                    staging.LinkedBenId = beneficiary.BenId;
                    await db.SaveChangesAsync();
                }

                ErrorMessage = initialStatus == WorkflowStatuses.Approved
                    ? "Beneficiary successfully added to the insurance system."
                    : "Beneficiary submitted and waiting for admin confirmation.";
                if (showMessage)
                {
                    MessageBox.Show(
                        existingBeneficiary is null
                            ? initialStatus == WorkflowStatuses.Approved
                                ? "Beneficiary approved and added to the insurance system."
                                : "Beneficiary submitted. Admin must confirm it before it becomes an official member."
                            : "This CRS beneficiary is already in insurance records. The record was linked and updated.",
                        "Beneficiary Added",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                SelectedSystemBeneficiary = beneficiary;
                ProfileBeneficiary = beneficiary;
                ResetLinkFormFields();
                await LoadAsync();
                
                // Keep the record selected to show the updated profile
                var reloadedRecord = staging is null
                    ? null
                    : _allRecords.FirstOrDefault(r => r.StagingId == staging.StagingId);
                SelectedRecord = reloadedRecord ?? staging;
                return true;
            }
            catch (Exception ex)
            {
                var detail = ex.GetBaseException().Message;
                ErrorMessage = $"Add beneficiary failed: {detail}";
                if (showMessage)
                {
                    MessageBox.Show(
                        ErrorMessage,
                        "Add Beneficiary Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                return false;
            }
        }

        private async Task LinkAsync()
        {
            await AddSelectedRecordToInsuranceAsync();
        }

        public async Task RefreshCurrentRecordAsync()
        {
            if (SelectedRecord is null) return;
            using var db = eSureHiDbContextFactory.Create();
            var reloaded = await db.BeneficiaryStaging
                .FirstOrDefaultAsync(b => b.StagingId == SelectedRecord.StagingId);
            if (reloaded != null)
            {
                SelectedRecord = reloaded;
            }
        }

        private static DateOnly? ParseDateOnlyOrNull(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var normalized = value.Trim();
            var supportedFormats = new[]
            {
                "yyyy-MM-dd",
                "yyyy-M-d",
                "MM/dd/yyyy",
                "M/d/yyyy",
                "yyyy/MM/dd",
                "dd/MM/yyyy",
                "d/M/yyyy"
            };

            if (DateOnly.TryParseExact(
                    normalized,
                    supportedFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var exactDate))
            {
                return exactDate;
            }

            if (DateOnly.TryParse(normalized, out var parsedDate))
                return parsedDate;

            if (DateTime.TryParse(normalized, out var parsedDateTime))
                return DateOnly.FromDateTime(parsedDateTime);

            return null;
        }

        private static string GetInitialMemberWorkflowStatus() =>
            PermissionService.CanApproveWorkflow
                ? WorkflowStatuses.Approved
                : WorkflowStatuses.Pending;

        private static string GetInitialMemberStatusRemarks(string status)
        {
            if (status == WorkflowStatuses.Approved)
                return "Validated and linked from CRS master list.";

            var user = AuthService.Instance.CurrentUser;
            var role = user?.Role ?? "User";
            var username = user?.Username ?? "unknown user";
            return $"Submitted by {role} ({username}); awaiting admin confirmation.";
        }

        // ── Skip ───────────────────────────────────────────────────────
        private async Task SkipAsync()
        {
            if (SelectedRecord is null) return;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var staging = await db.BeneficiaryStaging
                    .FindAsync(SelectedRecord.StagingId);
                if (staging is not null)
                {
                    staging.LinkStatus = "Skipped";
                    await db.SaveChangesAsync();
                }
                await LoadAsync();
            }
            catch (Exception ex) { ErrorMessage = $"Skip failed: {ex.Message}"; }
        }

        private void ClearFilters()
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));
            ApplyFilter();
        }

        public class RequirementStatus
        {
            public RequirementStatus(string name, bool isMet, bool isRequired = true)
            {
                Name = name;
                IsMet = isMet;
                IsRequired = isRequired;
            }

            public string Name { get; }
            public bool IsMet { get; }
            public bool IsRequired { get; }
            public string StatusText => IsMet ? "OK" : (IsRequired ? "Needed" : "Optional");
        }
    }
}
