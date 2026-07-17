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

        public ObservableCollection<Employee> Employees { get; } = new();
        public ObservableCollection<Employee> FilteredEmployees { get; } = new();

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
        public string AddMemberButtonText =>
            PermissionService.CanApproveWorkflow
                ? "ADD TO INSURANCE"
                : "SUBMIT FOR APPROVAL";

        public bool CanReview => 
            PermissionService.CanApproveWorkflow &&
            SelectedSystemBeneficiary != null && 
            SelectedSystemBeneficiary.WorkflowStatus == WorkflowStatuses.Pending;

        public bool CanApproveMember =>
            PermissionService.CanApproveWorkflow &&
            SelectedSystemBeneficiary != null &&
            SelectedSystemBeneficiary.WorkflowStatus is WorkflowStatuses.Pending or WorkflowStatuses.UnderReview;

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
            { "Spouse", "Child", "Parent", "Sibling", "Other" };
        public ObservableCollection<string> SourceOfFundsOptions { get; } = new()
        {
            "Job Order",
            "Casual",
            "Regular"
        };
        public string SourceOfFunds
        {
            get => _sourceOfFunds;
            set => SetProperty(ref _sourceOfFunds, value);
        }

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

                if (_selectionOnly)
                    QueueSelectionSearch();
                else
                    ApplyFilter();
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
                    ApplyFilter();
                    OnPropertyChanged(nameof(IsCrsSource));
                    OnPropertyChanged(nameof(IsSystemSource));
                    OnPropertyChanged(nameof(SourceCaption));
                    OnPropertyChanged(nameof(ActiveTotalCount));
                    OnPropertyChanged(nameof(ActiveFilteredCount));
                    _ = LoadAsync();
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
        public string ProfileAddress { get => _profileAddress; set => SetProperty(ref _profileAddress, value); }
        public string EligibilityStatus { get => _eligibilityStatus; set => SetProperty(ref _eligibilityStatus, value); }
        private string _qualificationReason = "Select a beneficiary to review qualification.";
        public string QualificationReason { get => _qualificationReason; set => SetProperty(ref _qualificationReason, value); }

        // ── State ──────────────────────────────────────────────────────
        private bool _isLoading;
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

        private CancellationTokenSource? _cts;
        private readonly bool _selectionOnly;
        private CancellationTokenSource? _selectionSearchCts;

        // ── Constructor ────────────────────────────────────────────────
        public BeneficiaryStagingViewModel(bool selectionOnly = false)
        {
            _selectionOnly = selectionOnly;
            ImportCommand = new RelayCommand(async () => await StartImportAsync(),
                                      () => !IsImporting);
            CancelImportCommand = new RelayCommand(CancelImport,
                                      () => IsImporting);
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
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

            if (_selectionOnly)
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
                await db.SaveChangesAsync();

                StatusRemarks = string.Empty;
                await LoadAsync();

                if (SelectedRecord != null)
                    await OpenProfileAsync(SelectedRecord);

                RefreshCanExecute();
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
                                (f.FundName == "Job Order" ||
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
                    ApplyFilter();
                    return;
                }

                // Remove automatic CRS master list sync/loading on view load to load local records instantly.
                // await AutoSyncCrsMasterListAsync();

                using var db = eSureHiDbContextFactory.Create();
                var records = await db.BeneficiaryStaging
                    .OrderBy(b => b.LastName)
                    .ThenBy(b => b.FirstName)
                    .ToListAsync();

                _allRecords.Clear();
                foreach (var r in records) _allRecords.Add(r);

                TotalCount = _allRecords.Count;
                UnlinkedCount = _allRecords.Count(r => r.LinkStatus == "Unlinked");
                LinkedCount = _allRecords.Count(r => r.LinkStatus == "Linked");
                await LoadSystemBeneficiariesAsync();
                ApplyFilter();
                
                if (TotalCount == 0 && !IsImporting)
                {
                    _ = StartImportAsync();
                }
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
            finally { IsLoading = false; }
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
                var query = db.BeneficiaryStaging.AsNoTracking();
                var search = SearchText.Trim();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.ToLower();
                    query = query.Where(r =>
                        (r.FullName != null && r.FullName.ToLower().Contains(s)) ||
                        (r.LastName != null && r.LastName.ToLower().Contains(s)) ||
                        (r.FirstName != null && r.FirstName.ToLower().Contains(s)) ||
                        (r.MiddleName != null && r.MiddleName.ToLower().Contains(s)) ||
                        (r.BeneficiaryId != null && r.BeneficiaryId.ToLower().Contains(s)) ||
                        (r.CivilRegistryId != null && r.CivilRegistryId.ToLower().Contains(s)) ||
                        (r.ResidentsId.HasValue && r.ResidentsId.Value.ToString().Contains(s)));
                }

                var records = await query
                    .OrderBy(r => r.LastName)
                    .ThenBy(r => r.FirstName)
                    .Take(80)
                    .ToListAsync(cancellationToken);

                DisplayedRecords.Clear();
                foreach (var record in records)
                    DisplayedRecords.Add(record);

                TotalCount = records.Count;
                FilteredCount = records.Count;
                UnlinkedCount = records.Count(r => r.LinkStatus == "Unlinked");
                LinkedCount = records.Count(r => r.LinkStatus == "Linked");
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

            DisplayedRecords.Clear();
            foreach (var r in q) DisplayedRecords.Add(r);
            FilteredCount = DisplayedRecords.Count;

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

            DisplayedSystemBeneficiaries.Clear();
            foreach (var b in local) DisplayedSystemBeneficiaries.Add(b);
            SystemFilteredCount = DisplayedSystemBeneficiaries.Count;
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

        private async Task OpenProfileAsync(BeneficiaryStaging record)
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            ResetLinkFormFields();
            ProfileBenefits.Clear();
            ProfileTransactions.Clear();
            ProfileCedulas.Clear();
            ProfileRequirements.Clear();
            ProfileBeneficiary = null;
            ProfileEmployee = null;
            ProfileAddress = record.Address ?? string.Empty;
            ProfileStatus = record.LinkStatus == "Linked" ? "Already in Insurance System" : "CRS master list record";
            ProfileCedulaStatus = "No cedula payment found";
            ProfileTransactionStatus = "Searching transactions...";
            EligibilityStatus = "Evaluating...";

            try
            {
                using var db = eSureHiDbContextFactory.Create();

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
                catch { /* GGMS offline? Ignore and continue with local data */ }

                ProfileTransactionStatus = ProfileTransactions.Any()
                    ? $"{ProfileTransactions.Count} transaction(s) found"
                    : "No transactions found";

                BuildRequirements(record);
                OnPropertyChanged(nameof(IsProfileOpen));
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load beneficiary profile failed: {ex.Message}";
            }
            finally { IsLoading = false; }
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

        private async Task AutoSyncCrsMasterListAsync()
        {
            if (_hasAutoSyncedCrs || IsImporting)
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
