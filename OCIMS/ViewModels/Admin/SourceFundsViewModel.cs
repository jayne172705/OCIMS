using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using MySqlConnector;

namespace eSureHi.ViewModels.Admin
{
    public enum SourceFundsLandingMode
    {
        Overview,
        BarangayFunds,
        GroupFunds,
        AllocatedFunds
    }

    public class SourceFundDisplayItem : ObservableObject
    {
        public SourceFund Fund { get; init; } = new();
        public int BeneficiaryCount { get; init; }
        public decimal BeneficiaryContribution { get; init; }

        public int SourceFundId => Fund.SourceFundId;
        public string FundName => Fund.FundName;
        public string FundType => Fund.FundType;
        public string Status => Fund.Status;
        public string? Description => Fund.Description;
        public decimal AllocatedAmount => Fund.AllocatedAmount;
        public decimal UsedAmount => Fund.UsedAmount + BeneficiaryContribution;
        public decimal RemainingAmount => AllocatedAmount - UsedAmount;
        public string? GgmsOfficeCode => Fund.GgmsOfficeCode;
    }

    public class FundReleaseLedgerItem
    {
        public string ReleaseDateText { get; init; } = string.Empty;
        public DateTime SortDate { get; init; }
        public string ReferenceNo { get; init; } = string.Empty;
        public string GgmsReference { get; init; } = string.Empty;
        public string RecipientName { get; init; } = string.Empty;
        public string ReleaseType { get; init; } = string.Empty;
        public string SourceOfFunds { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string Status { get; init; } = string.Empty;
        public string Details { get; init; } = string.Empty;
    }

    public class GgmsMatchItem
    {
        public string TransactionDateText { get; init; } = string.Empty;
        public string ProjectCode { get; init; } = string.Empty;
        public string ProjectName { get; init; } = string.Empty;
        public string RecipientName { get; init; } = string.Empty;
        public string TransactionType { get; init; } = string.Empty;
        public decimal Amount { get; init; }
        public string Status { get; init; } = string.Empty;
        public string MatchStatus { get; init; } = string.Empty;
    }

    public class TrackCoverageItem
    {
        public InsurancePolicy Policy { get; init; } = new();
        public int PolicyId => Policy.PolicyId;
        public string PolicyCode => Policy.PolicyCode;
        public string TrackName => Policy.PolicyName;
        public string TrackType => Policy.PolicyType;
        public string FundingOffice => Policy.ProviderName ?? string.Empty;
        public decimal BudgetAllocation => Policy.CoverageAmount;
        public DateOnly? EffectiveDate => Policy.EffectiveDate;
        public DateOnly? ExpiryDate => Policy.ExpiryDate;
        public string Status => Policy.PolicyStatus;
    }

    public class BarangayFundItem
    {
        public string Barangay { get; init; } = string.Empty;
        public string Status { get; init; } = "Active";
        public decimal Balance { get; init; }
        public string FundHealth => Balance <= 0 ? "Critical" : "Healthy";
        public string FundHealthDetail => Balance <= 0 ? "Low funds" : "Funds available";
        public string UpdatedText { get; init; } = "Updated 2 weeks ago";
        public int MemberCount { get; init; }
        public int PrimaryCount { get; init; }
        public int DependentCount { get; init; }
        public int ClaimCount { get; init; }
        public decimal TotalContribution { get; init; }
        public int GroupCount { get; init; }
        public string GroupsText { get; init; } = string.Empty;
    }

    public class GroupFundItem
    {
        public string Name { get; init; } = string.Empty;
        public string Status { get; init; } = "Active";
        public decimal Balance { get; init; }
        public string AssignedTo { get; init; } = "Manager One";
        public string FundHealth => Balance <= 0 ? "Critical" : "Healthy";
        public string FundHealthDetail => Balance <= 0 ? "Low funds" : "Funds available";
        public string UpdatedText { get; init; } = "Updated 2 weeks ago";
    }

    public class SourceFundsViewModel : ObservableObject
    {
        private static readonly string[] EmployeeTrackFunds = { "Job Order", "Casual", "Regular" };
        private static readonly (string Code, string Name, string Type, decimal Budget)[] EmployeeTrackDefinitions =
        {
            ("POL-JOBORDER-001", "Job Order", "Job Order", 250000m),
            ("POL-CASUAL-001", "Casual", "Casual", 500000m),
            ("POL-REGULAR-001", "Regular", "Regular", 1000000m)
        };
        private static readonly string[] BarangayNames =
        {
            "Balasinon",
            "Buguis",
            "Carre",
            "Clib",
            "Harada Butai",
            "Katipunan",
            "Kiblagon",
            "Labon",
            "Laperas",
            "Lapla",
            "Litos",
            "Luparan",
            "Mckinley",
            "New Cebu",
            "Osmeña",
            "Palili",
            "Parame",
            "Poblacion",
            "Roxas",
            "Solongvale",
            "Tagolilong",
            "Tala-o",
            "Talas",
            "Tanwalang",
            "Waterfall"
        };

        private SourceFundDisplayItem? _selectedFund;
        private TrackCoverageItem? _selectedTrackCoverage;
        private readonly SourceFundsLandingMode _landingMode;
        private string _searchText = string.Empty;
        private string _fundName = string.Empty;
        private string _fundType = "Employee Track";
        private string _description = string.Empty;
        private decimal _allocatedAmount;
        private string _ggmsOfficeCode = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isLoading;
        private decimal _ggmsAllocated;
        private decimal _ggmsSpent;
        private decimal _ggmsRemaining;
        private string _ggmsStatus = "Checking GGMS...";
        private int _selectedSectionIndex;

        public ObservableCollection<SourceFundDisplayItem> Funds { get; } = new();
        public ObservableCollection<SourceFundDisplayItem> DisplayedFunds { get; } = new();
        public ObservableCollection<BarangayFundItem> BarangayFunds { get; } = new();
        public ObservableCollection<BarangayFundItem> DisplayedBarangayFunds { get; } = new();
        public ObservableCollection<GroupFundItem> GroupFunds { get; } = new();
        public ObservableCollection<FundReleaseLedgerItem> ReleaseLedger { get; } = new();
        public ObservableCollection<GgmsMatchItem> GgmsMatches { get; } = new();
        public ObservableCollection<TrackCoverageItem> TrackCoverages { get; } = new();
        public ObservableCollection<Beneficiary> SelectedGroupBeneficiaries { get; } = new();

        private string _selectedGroupFundName = string.Empty;
        public string SelectedGroupFundName
        {
            get => _selectedGroupFundName;
            set => SetProperty(ref _selectedGroupFundName, value);
        }

        private bool _isGroupBeneficiaryDialogOpen;
        public bool IsGroupBeneficiaryDialogOpen
        {
            get => _isGroupBeneficiaryDialogOpen;
            set => SetProperty(ref _isGroupBeneficiaryDialogOpen, value);
        }

        public string[] FundTypes { get; } = { "Employee Track" };
        public string PageTitle =>
            _landingMode switch
            {
                SourceFundsLandingMode.BarangayFunds => "Barangay Funds",
                SourceFundsLandingMode.GroupFunds => "Group Funds",
                SourceFundsLandingMode.AllocatedFunds => "Allocated Funds",
                _ => "Budget Funds"
            };
        public string PageSubtitle =>
            _landingMode switch
            {
                SourceFundsLandingMode.BarangayFunds => "Distribution of member funding by barangay",
                SourceFundsLandingMode.GroupFunds => "Assigned Job Order, Casual, and Regular budget sources",
                SourceFundsLandingMode.AllocatedFunds => "Configured allocation coverage and track budgets",
                _ => "Budget sources, coverage allocations, and GGMS matching"
            };
        public bool IsBarangayLanding => _landingMode == SourceFundsLandingMode.BarangayFunds;
        public bool IsGroupLanding => _landingMode == SourceFundsLandingMode.GroupFunds;
        public bool IsStandardLanding => !IsBarangayLanding && !IsGroupLanding;
        public bool CanReleaseBarangayFunds =>
            PermissionService.IsAdminReviewer(AuthService.Instance.CurrentUser?.Role);
        public int GroupFundCount => GroupFunds.Count;
        public decimal GroupFundBalance => GroupFunds.Sum(item => item.Balance);
        public int AssignedGroupFundCount => GroupFunds.Count(item => !string.IsNullOrWhiteSpace(item.AssignedTo));
        public int UnassignedGroupFundCount => GroupFundCount - AssignedGroupFundCount;

        public SourceFundDisplayItem? SelectedFund
        {
            get => _selectedFund;
            set
            {
                if (SetProperty(ref _selectedFund, value))
                {
                    LoadSelectedToForm();
                    SaveCommand.RaiseCanExecuteChanged();
                    DeleteCommand.RaiseCanExecuteChanged();
                    ToggleStatusCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyFilter();
            }
        }

        public TrackCoverageItem? SelectedTrackCoverage
        {
            get => _selectedTrackCoverage;
            set
            {
                if (SetProperty(ref _selectedTrackCoverage, value))
                {
                    EditTrackCoverageCommand.RaiseCanExecuteChanged();
                    CancelTrackCoverageCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string FundName
        {
            get => _fundName;
            set
            {
                SetProperty(ref _fundName, value);
                SaveCommand.RaiseCanExecuteChanged();
            }
        }

        public string FundType { get => _fundType; set => SetProperty(ref _fundType, value); }
        public string Description { get => _description; set => SetProperty(ref _description, value); }
        public decimal AllocatedAmount { get => _allocatedAmount; set => SetProperty(ref _allocatedAmount, value); }
        public string GgmsOfficeCode { get => _ggmsOfficeCode; set => SetProperty(ref _ggmsOfficeCode, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public decimal TotalAllocated => Funds.Sum(f => f.AllocatedAmount);
        public decimal TotalUsed => Funds.Sum(f => f.UsedAmount);
        public decimal TotalRemaining => TotalAllocated - TotalUsed;
        public decimal TotalReleasedBySource => ReleaseLedger.Sum(r => r.Amount);
        public int GgmsMatchedCount => GgmsMatches.Count(g => g.MatchStatus == "Matched");
        public int GgmsUnmatchedCount => GgmsMatches.Count(g => g.MatchStatus != "Matched");
        public int TrackCoverageCount => TrackCoverages.Count;
        public decimal GgmsAllocated { get => _ggmsAllocated; set => SetProperty(ref _ggmsAllocated, value); }
        public decimal GgmsSpent { get => _ggmsSpent; set => SetProperty(ref _ggmsSpent, value); }
        public decimal GgmsRemaining { get => _ggmsRemaining; set => SetProperty(ref _ggmsRemaining, value); }
        public string GgmsStatus { get => _ggmsStatus; set => SetProperty(ref _ggmsStatus, value); }
        public int SelectedSectionIndex
        {
            get => _selectedSectionIndex;
            set => SetProperty(ref _selectedSectionIndex, value);
        }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand NewCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand ToggleStatusCommand { get; }
        public RelayCommand ClearSearchCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }
        public RelayCommand EditTrackCoverageCommand { get; }
        public RelayCommand CancelTrackCoverageCommand { get; }
        public RelayCommand<BarangayFundItem> ReleaseBarangayFundCommand { get; }
        public RelayCommand<BarangayFundItem> OpenFamilyHeadsCommand { get; }
        public RelayCommand SyncGgmsFundsCommand { get; }
        public RelayCommand<GroupFundItem> ViewBeneficiariesCommand { get; }
        public RelayCommand CloseBeneficiariesCommand { get; }
        public RelayCommand AddGroupBeneficiaryCommand { get; }

        public SourceFundsViewModel()
            : this(SourceFundsLandingMode.Overview)
        {
        }

        public SourceFundsViewModel(SourceFundsLandingMode landingMode)
        {
            _landingMode = landingMode;
            SelectedSectionIndex = landingMode switch
            {
                SourceFundsLandingMode.BarangayFunds => 0,
                SourceFundsLandingMode.GroupFunds => 1,
                SourceFundsLandingMode.AllocatedFunds => 2,
                _ => 1
            };
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            NewCommand = new RelayCommand(ClearForm);
            SaveCommand = new RelayCommand(async () => await SaveAsync(), CanSave);
            DeleteCommand = new RelayCommand(async () => await DeleteAsync(), () => SelectedFund is not null);
            ToggleStatusCommand = new RelayCommand(async () => await ToggleStatusAsync(), () => SelectedFund is not null);
            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
            ExportCommand = new RelayCommand(ExportReport);
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);
            EditTrackCoverageCommand = new RelayCommand(OpenTrackCoverageDialog, () => SelectedTrackCoverage is not null);
            CancelTrackCoverageCommand = new RelayCommand(async () => await CancelTrackCoverageAsync(),
                () => SelectedTrackCoverage is not null && SelectedTrackCoverage.Status == "Active");
            ReleaseBarangayFundCommand = new RelayCommand<BarangayFundItem>(OpenBarangayReleaseDialog,
                item => item is not null && CanReleaseBarangayFunds);
            OpenFamilyHeadsCommand = new RelayCommand<BarangayFundItem>(OpenFamilyHeadsList,
                item => item is not null);
            SyncGgmsFundsCommand = new RelayCommand(async () => await SyncGgmsFundsAsync(), () => GgmsAllocated > 0);
            ViewBeneficiariesCommand = new RelayCommand<GroupFundItem>(async item => { if (item != null) await ViewBeneficiariesAsync(item); });
            CloseBeneficiariesCommand = new RelayCommand(() => IsGroupBeneficiaryDialogOpen = false);
            AddGroupBeneficiaryCommand = new RelayCommand(async () => await AddGroupBeneficiaryAsync(), () => !string.IsNullOrEmpty(SelectedGroupFundName));

            _ = LoadAsync();
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }

        private async Task ViewBeneficiariesAsync(GroupFundItem item)
        {
            if (item == null) return;
            SelectedGroupFundName = item.Name;
            SelectedGroupBeneficiaries.Clear();
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var beneficiaries = await db.Beneficiaries
                    .Include(b => b.Employee)
                    .Where(b => b.Employee != null && b.Employee.EmploymentType == item.Name)
                    .ToListAsync();
                    
                foreach (var b in beneficiaries)
                {
                    SelectedGroupBeneficiaries.Add(b);
                }
                IsGroupBeneficiaryDialogOpen = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading beneficiaries: {ex.Message}";
            }
        }

        private async Task AddGroupBeneficiaryAsync()
        {
            var vm = new BeneficiaryStagingViewModel(true);
            var dialog = new Views.Admin.Dialogs.BeneficiarySearchDialog(vm);
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;

            if (dialog.ShowDialog() == true && vm.SelectedRecord is not null)
            {
                var crsRecord = vm.SelectedRecord;
                try
                {
                    using var db = eSureHiDbContextFactory.Create();
                    
                    var existingBen = await db.Beneficiaries.FirstOrDefaultAsync(b => b.BeneficiaryId == crsRecord.BeneficiaryId);
                    if (existingBen != null)
                    {
                        MessageBox.Show("This CRS record is already an active beneficiary in the system.", "Add Beneficiary", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var employee = new Employee
                    {
                        EmployeeNo = "EMP-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                        FirstName = crsRecord.FirstName ?? string.Empty,
                        LastName = crsRecord.LastName ?? string.Empty,
                        MiddleName = crsRecord.MiddleName,
                        DateOfBirth = DateOnly.TryParse(crsRecord.DateOfBirth, out var dob) ? dob : null,
                        Gender = crsRecord.Sex,
                        CivilStatus = string.IsNullOrWhiteSpace(crsRecord.MaritalStatus) ? "Single" : crsRecord.MaritalStatus,
                        Barangay = crsRecord.Address,
                        EmploymentType = SelectedGroupFundName,
                        EmploymentStatus = "Active",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    db.Employees.Add(employee);
                    await db.SaveChangesAsync();

                    var beneficiary = new Beneficiary
                    {
                        EmpId = employee.EmpId,
                        BeneficiaryId = crsRecord.BeneficiaryId,
                        FirstName = employee.FirstName,
                        LastName = employee.LastName,
                        Gender = employee.Gender,
                        DateOfBirth = employee.DateOfBirth,
                        WorkflowStatus = "Approved",
                        IsPrimary = true,
                        CreatedAt = DateTime.Now,
                        SourceOfFunds = SelectedGroupFundName,
                        CivilRegistryId = crsRecord.BeneficiaryId,
                        IsActive = true
                    };
                    db.Beneficiaries.Add(beneficiary);
                    await db.SaveChangesAsync();

                    StatusMessage = $"Added {beneficiary.FullName} to {SelectedGroupFundName} track.";
                    
                    var groupItem = GroupFunds.FirstOrDefault(g => g.Name == SelectedGroupFundName);
                    if (groupItem != null)
                    {
                        await ViewBeneficiariesAsync(groupItem);
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to add beneficiary: {ex.Message}";
                }
            }
        }

        private async Task LoadAsync()
        {
            IsLoading = true;
            StatusMessage = string.Empty;
            try
            {
                await EnsureSourceFundsSchemaAsync();
                using var db = eSureHiDbContextFactory.Create();
                await EnsureDefaultFundsAsync(db);
                await EnsureEmployeeTrackPoliciesAsync(db);

                var usageRows = await db.Beneficiaries
                    .Where(b => b.SourceOfFunds != null && b.SourceOfFunds != "")
                    .Select(g => new
                    {
                        Source = g.SourceOfFunds!,
                        g.Contribution
                    })
                    .ToListAsync();

                var usage = usageRows
                    .GroupBy(row => row.Source, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new
                    {
                        Source = group.Key,
                        Count = group.Count(),
                        Contribution = group.Sum(row => row.Contribution)
                    })
                    .ToList();

                var funds = (await db.SourceFunds
                    .Where(f => EmployeeTrackFunds.Contains(f.FundName))
                    .OrderByDescending(f => f.Status == "Active")
                    .ToListAsync())
                    .OrderBy(f => Array.IndexOf(EmployeeTrackFunds, f.FundName))
                    .ToList();

                Funds.Clear();
                foreach (var fund in funds)
                {
                    var matched = usage.FirstOrDefault(u =>
                        string.Equals(u.Source, fund.FundName, StringComparison.OrdinalIgnoreCase));
                    Funds.Add(new SourceFundDisplayItem
                    {
                        Fund = fund,
                        BeneficiaryCount = matched?.Count ?? 0,
                        BeneficiaryContribution = matched?.Contribution ?? 0
                    });
                }

                LoadGroupFunds();
                await LoadBarangayFundsAsync(db);
                ApplyFilter();
                OnTotalsChanged();
                await LoadTrackCoverageAsync(db);
                await LoadReleaseLedgerAsync(db);
                await LoadGgmsAsync();
                await LoadGgmsMatchesAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadGroupFunds()
        {
            GroupFunds.Clear();

            var groupNames = new[] { "Casual", "Job Order", "Captain", "Regular" };
            foreach (var name in groupNames)
            {
                var source = Funds.FirstOrDefault(f =>
                    string.Equals(f.FundName, name, StringComparison.OrdinalIgnoreCase));

                GroupFunds.Add(new GroupFundItem
                {
                    Name = name,
                    Status = source?.Status ?? "Active",
                    Balance = source?.RemainingAmount ?? 0,
                    AssignedTo = "Manager One"
                });
            }

            OnPropertyChanged(nameof(GroupFundCount));
            OnPropertyChanged(nameof(GroupFundBalance));
            OnPropertyChanged(nameof(AssignedGroupFundCount));
            OnPropertyChanged(nameof(UnassignedGroupFundCount));
        }

        private async Task LoadBarangayFundsAsync(eSureHiDbContext db)
        {
            var beneficiaries = await db.Beneficiaries
                .Include(b => b.Employee)
                .Where(b => b.IsActive)
                .ToListAsync();

            var claimCounts = await db.Claims
                .Where(c => c.BenId.HasValue)
                .GroupBy(c => c.BenId!.Value)
                .Select(g => new { BenId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.BenId, g => g.Count);

            BarangayFunds.Clear();
            var barangayBalances = await db.SourceFunds
                .Where(f => f.FundType == "Barangay")
                .ToDictionaryAsync(
                    f => f.FundName,
                    f => f.AllocatedAmount - f.UsedAmount,
                    StringComparer.OrdinalIgnoreCase);

            var summaries = beneficiaries
                .GroupBy(b => NormalizeBarangay(b.Employee?.Barangay))
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var assignedGroups = group
                            .Select(b => FirstNonEmpty(
                                b.SourceOfFunds,
                                b.Employee?.EmploymentType,
                                "Unassigned"))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(value => value)
                            .ToArray();

                        return new
                         {
                            MemberCount = group.Count(),
                            PrimaryCount = group.Count(b => b.IsPrimary),
                            DependentCount = group.Count(b => !b.IsPrimary),
                            ClaimCount = group.Sum(b => claimCounts.TryGetValue(b.BenId, out var count) ? count : 0),
                            TotalContribution = group.Sum(b => b.Contribution),
                            GroupCount = assignedGroups.Length,
                            GroupsText = string.Join(", ", assignedGroups)
                        };
                    },
                    StringComparer.OrdinalIgnoreCase);

            foreach (var barangay in BarangayNames)
            {
                summaries.TryGetValue(barangay, out var summary);
                BarangayFunds.Add(new BarangayFundItem
                {
                    Barangay = barangay,
                    Balance = barangayBalances.TryGetValue(BuildBarangayFundName(barangay), out var balance)
                        ? balance
                        : 0,
                    MemberCount = summary?.MemberCount ?? 0,
                    PrimaryCount = summary?.PrimaryCount ?? 0,
                    DependentCount = summary?.DependentCount ?? 0,
                    ClaimCount = summary?.ClaimCount ?? 0,
                    TotalContribution = summary?.TotalContribution ?? 0,
                    GroupCount = summary?.GroupCount ?? 0,
                    GroupsText = summary?.GroupsText ?? string.Empty
                });
            }
        }

        private void OpenFamilyHeadsList(BarangayFundItem? barangay)
        {
            if (barangay is null)
                return;

            var dialog = new Views.Admin.Dialogs.FamilyHeadsDialog(barangay.Barangay);
            dialog.ShowDialog();
        }

        private void OpenBarangayReleaseDialog(BarangayFundItem? barangay)
        {
            if (barangay is null || !CanReleaseBarangayFunds)
                return;

            var options = Funds
                .Where(f => f.Status == "Active" && f.RemainingAmount > 0)
                .Select(f => new Views.Admin.Dialogs.BarangayFundSourceOption
                {
                    SourceFundId = f.SourceFundId,
                    Name = f.FundName,
                    AvailableBalance = f.RemainingAmount
                })
                .ToList();

            if (options.Count == 0)
            {
                MessageBox.Show(
                    "There is no active source fund with an available balance.",
                    "Release Barangay Fund",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new Views.Admin.Dialogs.BarangayFundReleaseDialog(barangay.Barangay, barangay.Balance, options);
            if (App.ActiveShell is not null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;

            if (dialog.ShowDialog() == true)
                _ = ReleaseBarangayFundAsync(barangay, dialog);
        }

        private async Task ReleaseBarangayFundAsync(
            BarangayFundItem barangay,
            Views.Admin.Dialogs.BarangayFundReleaseDialog dialog)
        {
            IsLoading = true;
            StatusMessage = string.Empty;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var source = await db.SourceFunds
                    .FirstOrDefaultAsync(f => f.SourceFundId == dialog.SourceFundId);

                if (source is null || source.Status != "Active")
                    throw new InvalidOperationException("The selected source fund is no longer active.");

                if (dialog.Amount > source.RemainingAmount)
                    throw new InvalidOperationException(
                        $"Release amount exceeds the available {source.FundName} balance of {source.RemainingAmount:N2}.");

                var barangayFundName = BuildBarangayFundName(barangay.Barangay);
                var destination = await db.SourceFunds
                    .FirstOrDefaultAsync(f => f.FundName == barangayFundName);

                if (destination is null)
                {
                    destination = new SourceFund
                    {
                        FundName = barangayFundName,
                        FundType = "Barangay",
                        Description = $"Barangay fund for {barangay.Barangay}",
                        Status = "Active",
                        CreatedAt = DateTime.Now
                    };
                    db.SourceFunds.Add(destination);
                }

                source.UsedAmount += dialog.Amount;
                source.UpdatedAt = DateTime.Now;
                destination.AllocatedAmount += dialog.Amount;
                destination.UpdatedAt = DateTime.Now;

                await db.SaveChangesAsync();
                await AuditService.LogInsert(
                    "barangay_fund_release",
                    destination.SourceFundId,
                    $"Released {dialog.Amount:N2} from {source.FundName} to Barangay {barangay.Barangay}. " +
                    $"Reference: {dialog.ReferenceNumber}. Date: {dialog.ReleaseDate:yyyy-MM-dd}. " +
                    $"Purpose: {dialog.Purpose}. Remarks: {dialog.Remarks}");

                StatusMessage = $"{dialog.Amount:N2} was released to Barangay {barangay.Barangay}.";
                await LoadAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Release failed: {ex.Message}";
                MessageBox.Show(
                    StatusMessage,
                    "Release Barangay Fund",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static string BuildBarangayFundName(string barangay) =>
            $"Barangay: {barangay}";

        private async Task LoadReleaseLedgerAsync(eSureHiDbContext db)
        {
            var claims = await db.Claims
                .Include(c => c.Employee)
                .Include(c => c.Beneficiary)
                .Where(c => c.ClaimStatus == "Released" && c.AmountReleased > 0)
                .OrderByDescending(c => c.ReleasedDate)
                .Take(300)
                .ToListAsync();

            var benefits = await db.Benefits
                .Include(b => b.EmployeePolicy)
                    .ThenInclude(ep => ep!.Employee)
                .Include(b => b.EmployeePolicy)
                    .ThenInclude(ep => ep!.Policy)
                .Where(b => b.UsedBenefit > 0)
                .OrderByDescending(b => b.LastUsedDate)
                .Take(300)
                .ToListAsync();

            ReleaseLedger.Clear();

            foreach (var claim in claims)
            {
                var releaseDate = claim.ReleasedDate ?? claim.ApprovedDate ?? claim.CreatedAt;
                ReleaseLedger.Add(new FundReleaseLedgerItem
                {
                    ReleaseDateText = releaseDate.ToString("MMM dd, yyyy"),
                    SortDate = releaseDate,
                    ReferenceNo = string.IsNullOrWhiteSpace(claim.ClaimNo) ? $"CLM-{claim.ClaimId:000000}" : claim.ClaimNo,
                    GgmsReference = BuildGgmsReference(claim.ClaimId),
                    RecipientName = claim.Beneficiary?.FullName ?? claim.Employee?.FullName ?? "Unknown recipient",
                    ReleaseType = string.IsNullOrWhiteSpace(claim.ClaimType) ? "Insurance Claim" : claim.ClaimType,
                    SourceOfFunds = string.IsNullOrWhiteSpace(claim.SourceOfFunds) ? "Unspecified" : claim.SourceOfFunds,
                    Amount = claim.AmountReleased,
                    Status = claim.ClaimStatus,
                    Details = claim.IncidentDescription ?? claim.Remarks ?? string.Empty
                });
            }

            foreach (var benefit in benefits)
            {
                var releaseDate = benefit.LastUsedDate?.ToDateTime(TimeOnly.MinValue) ?? benefit.UpdatedAt;
                var employee = benefit.EmployeePolicy?.Employee;
                var policy = benefit.EmployeePolicy?.Policy;
                ReleaseLedger.Add(new FundReleaseLedgerItem
                {
                    ReleaseDateText = releaseDate.ToString("MMM dd, yyyy"),
                    SortDate = releaseDate,
                    ReferenceNo = $"BEN-{benefit.BenefitId:000000}",
                    GgmsReference = BuildGgmsReference(benefit.BenefitId),
                    RecipientName = employee?.FullName ?? "Unknown employee",
                    ReleaseType = string.IsNullOrWhiteSpace(benefit.BenefitType) ? "Insurance Benefit" : benefit.BenefitType,
                    SourceOfFunds = string.IsNullOrWhiteSpace(benefit.SourceOfFunds) ? "Unspecified" : benefit.SourceOfFunds,
                    Amount = benefit.UsedBenefit,
                    Status = "Used",
                    Details = policy?.PolicyName ?? benefit.Notes ?? string.Empty
                });
            }

            var sorted = ReleaseLedger.OrderByDescending(r => r.SortDate).ToList();
            ReleaseLedger.Clear();
            foreach (var item in sorted)
                ReleaseLedger.Add(item);

            OnPropertyChanged(nameof(TotalReleasedBySource));
        }

        private static async Task EnsureDefaultFundsAsync(eSureHiDbContext db)
        {
            foreach (var track in EmployeeTrackDefinitions)
            {
                var fund = await db.SourceFunds
                    .FirstOrDefaultAsync(f => f.FundName == track.Name);

                if (fund is null)
                {
                    fund = new SourceFund
                    {
                        FundName = track.Name,
                        CreatedAt = DateTime.Now
                    };
                    db.SourceFunds.Add(fund);
                }

                fund.FundType = "Employee Track";
                fund.Description = $"Budget allocation for {track.Name} employee applicants and assignments.";
                fund.GgmsOfficeCode = track.Name == "Casual" ? null : "OFF-2026-0004";
                fund.Status = "Active";
                if (fund.AllocatedAmount <= 0)
                    fund.AllocatedAmount = track.Budget;
                fund.UpdatedAt = DateTime.Now;
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureEmployeeTrackPoliciesAsync(eSureHiDbContext db)
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(DateTime.Today);
            var nextYear = today.AddYears(1);
            var reviewDate = nextYear.AddMonths(-1);

            foreach (var track in EmployeeTrackDefinitions)
            {
                var policy = await db.InsurancePolicies
                    .FirstOrDefaultAsync(p => p.PolicyCode == track.Code);

                if (policy is null)
                {
                    policy = new InsurancePolicy
                    {
                        PolicyCode = track.Code,
                        CreatedBy = AuthService.Instance.CurrentUser?.UserId,
                        CreatedAt = now
                    };
                    db.InsurancePolicies.Add(policy);
                }

                policy.PolicyName = track.Name;
                policy.PolicyType = track.Type;
                policy.ProviderName = "LGU Sulop HRMO / Budget Office";
                policy.ProviderContact = "(082) 123-4567";
                policy.EffectiveDate = today;
                policy.ExpiryDate = nextYear;
                policy.RenewalDate = reviewDate;
                policy.CoverageAmount = track.Budget;
                policy.Description = $"Budget track for {track.Name} employee applications and assignments.";
                policy.TermsConditions = "When this budget is depleted, request additional funding through GGMS.";
                policy.PolicyStatus = "Active";
                policy.UpdatedAt = now;
            }

            await db.SaveChangesAsync();
        }

        private async Task LoadTrackCoverageAsync(eSureHiDbContext db)
        {
            var trackCodes = EmployeeTrackDefinitions.Select(t => t.Code).ToArray();
            var policies = await db.InsurancePolicies
                .Where(p => trackCodes.Contains(p.PolicyCode))
                .ToListAsync();

            TrackCoverages.Clear();
            foreach (var policy in policies.OrderBy(p => Array.IndexOf(EmployeeTrackFunds, p.PolicyType)))
                TrackCoverages.Add(new TrackCoverageItem { Policy = policy });

            OnPropertyChanged(nameof(TrackCoverageCount));
        }

        private static async Task EnsureSourceFundsSchemaAsync()
        {
            await LocalDatabaseInitializer.InitializeAsync();
        }

        private static async Task EnsureSourceColumnAsync(MySqlConnection conn, string tableName)
        {
            await using var check = conn.CreateCommand();
            check.CommandText = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @table_name
                  AND COLUMN_NAME = 'source_of_funds';";
            check.Parameters.AddWithValue("@table_name", tableName);

            if (Convert.ToInt32(await check.ExecuteScalarAsync()) > 0)
                return;

            await using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE `{tableName}` ADD COLUMN source_of_funds VARCHAR(120) NULL;";
            await alter.ExecuteNonQueryAsync();
        }

        private static async Task SeedSourceFundAsync(
            MySqlConnection conn,
            string fundName,
            string fundType,
            string description,
            string? ggmsOfficeCode)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO source_funds
                    (fund_name, fund_type, description, status, ggms_office_code)
                VALUES
                    (@fund_name, @fund_type, @description, 'Active', @ggms_office_code)
                ON DUPLICATE KEY UPDATE
                    fund_type = VALUES(fund_type),
                    description = VALUES(description),
                    status = 'Active',
                    ggms_office_code = VALUES(ggms_office_code);";
            cmd.Parameters.AddWithValue("@fund_name", fundName);
            cmd.Parameters.AddWithValue("@fund_type", fundType);
            cmd.Parameters.AddWithValue("@description", description);
            cmd.Parameters.AddWithValue("@ggms_office_code", (object?)ggmsOfficeCode ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task LoadGgmsAsync()
        {
            var summary = await GgmsService.GetFundSummaryAsync();
            if (summary.IsLoaded)
            {
                GgmsAllocated = summary.AllocatedAmount;
                GgmsSpent = summary.SpentAmount;
                GgmsRemaining = summary.RemainingAmount;
                GgmsStatus = string.IsNullOrWhiteSpace(summary.ErrorMessage)
                    ? $"GGMS online for {summary.Year}"
                    : summary.ErrorMessage;
            }
            else
            {
                GgmsAllocated = 0;
                GgmsSpent = 0;
                GgmsRemaining = 0;
                GgmsStatus = summary.ErrorMessage;
            }
            SyncGgmsFundsCommand.RaiseCanExecuteChanged();
        }

        private async Task SyncGgmsFundsAsync()
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var funds = await db.SourceFunds.Where(f => f.FundName == "Regular" || f.FundName == "Casual" || f.FundName == "Job Order").ToListAsync();
                
                var totalAllocated = funds.Sum(f => f.AllocatedAmount);
                if (GgmsAllocated > totalAllocated)
                {
                    var extra = GgmsAllocated - totalAllocated;
                    var extraPerTrack = extra / 3;
                    foreach (var fund in funds)
                    {
                        fund.AllocatedAmount += extraPerTrack;
                        fund.UpdatedAt = DateTime.Now;
                    }
                    await db.SaveChangesAsync();
                    StatusMessage = $"Successfully synced {extra:N2} funds from GGMS.";
                    await LoadAsync();
                }
                else
                {
                    StatusMessage = "Local funds are already up to date with GGMS.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Sync failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadGgmsMatchesAsync()
        {
            GgmsMatches.Clear();

            try
            {
                var connStr = GgmsDbContextFactory.GetConnectionString();
                await using var conn = new MySqlConnection(connStr);
                await conn.OpenAsync();

                await using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, project_code, project_name, office_id, full_name,
                           transaction_type, amount, transaction_date, status
                    FROM consolidated_transactions
                    WHERE (project_code LIKE 'IMS-%' OR office_id = 'OFF-2026-0004')
                    ORDER BY transaction_date DESC, id DESC
                    LIMIT 300;";

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var projectCode = ReadString(reader, "project_code");
                    var hasLocalRelease = ReleaseLedger.Any(r =>
                        string.Equals(r.GgmsReference, projectCode, StringComparison.OrdinalIgnoreCase));

                    var transactionDate = ReadDate(reader, "transaction_date");
                    GgmsMatches.Add(new GgmsMatchItem
                    {
                        TransactionDateText = transactionDate?.ToString("MMM dd, yyyy") ?? string.Empty,
                        ProjectCode = projectCode,
                        ProjectName = ReadString(reader, "project_name"),
                        RecipientName = ReadString(reader, "full_name"),
                        TransactionType = ReadString(reader, "transaction_type"),
                        Amount = ReadDecimal(reader, "amount"),
                        Status = ReadString(reader, "status"),
                        MatchStatus = hasLocalRelease ? "Matched" : "GGMS only"
                    });
                }
            }
            catch (Exception ex)
            {
                GgmsStatus = $"GGMS matching unavailable: {ex.Message}";
            }
            finally
            {
                OnPropertyChanged(nameof(GgmsMatchedCount));
                OnPropertyChanged(nameof(GgmsUnmatchedCount));
            }
        }

        private void ApplyFilter()
        {
            DisplayedFunds.Clear();
            var query = Funds.AsEnumerable();
            var search = SearchText.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(f =>
                    f.FundName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    f.FundType.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (f.Description ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in query)
                DisplayedFunds.Add(item);

            DisplayedBarangayFunds.Clear();
            var barangayQuery = BarangayFunds.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                barangayQuery = barangayQuery.Where(item =>
                    item.Barangay.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    item.GroupsText.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in barangayQuery)
                DisplayedBarangayFunds.Add(item);
        }

        private void LoadSelectedToForm()
        {
            if (SelectedFund is null)
                return;

            FundName = SelectedFund.Fund.FundName;
            FundType = SelectedFund.Fund.FundType;
            Description = SelectedFund.Fund.Description ?? string.Empty;
            AllocatedAmount = SelectedFund.Fund.AllocatedAmount;
            GgmsOfficeCode = SelectedFund.Fund.GgmsOfficeCode ?? string.Empty;
            StatusMessage = string.Empty;
        }

        private void ClearForm()
        {
            SelectedFund = null;
            FundName = string.Empty;
            FundType = "Employee Track";
            Description = string.Empty;
            AllocatedAmount = 0;
            GgmsOfficeCode = string.Empty;
            StatusMessage = string.Empty;
            SaveCommand.RaiseCanExecuteChanged();
        }

        private bool CanSave() =>
            EmployeeTrackFunds.Any(f => string.Equals(f, FundName.Trim(), StringComparison.OrdinalIgnoreCase));

        private async Task SaveAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var name = FundName.Trim();
                if (!EmployeeTrackFunds.Any(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase)))
                {
                    StatusMessage = "Only Job Order, Casual, and Regular source funds are allowed.";
                    return;
                }

                name = EmployeeTrackFunds.First(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase));
                var duplicate = await db.SourceFunds.AnyAsync(f =>
                    f.FundName == name &&
                    (SelectedFund == null || f.SourceFundId != SelectedFund.SourceFundId));
                if (duplicate)
                {
                    StatusMessage = "A source of funds with this name already exists.";
                    return;
                }

                SourceFund fund;
                if (SelectedFund is null)
                {
                    fund = new SourceFund { CreatedAt = DateTime.Now, Status = "Active" };
                    db.SourceFunds.Add(fund);
                }
                else
                {
                    fund = await db.SourceFunds.FindAsync(SelectedFund.SourceFundId) ?? new SourceFund();
                }

                fund.FundName = name;
                fund.FundType = string.IsNullOrWhiteSpace(FundType) ? "Other" : FundType.Trim();
                fund.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
                fund.AllocatedAmount = AllocatedAmount;
                fund.GgmsOfficeCode = string.IsNullOrWhiteSpace(GgmsOfficeCode) ? null : GgmsOfficeCode.Trim();
                fund.UpdatedAt = DateTime.Now;

                await db.SaveChangesAsync();
                StatusMessage = "Source of funds saved.";
                await LoadAsync();
                SelectedFund = Funds.FirstOrDefault(f => f.FundName == fund.FundName);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Save failed: {ex.Message}";
            }
        }

        private async Task ToggleStatusAsync()
        {
            if (SelectedFund is null)
                return;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var fund = await db.SourceFunds.FindAsync(SelectedFund.SourceFundId);
                if (fund is null)
                    return;

                fund.Status = fund.Status == "Active" ? "Inactive" : "Active";
                fund.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();
                StatusMessage = $"Source marked {fund.Status}.";
                await LoadAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Toggle failed: {ex.Message}";
            }
        }

        private void OpenTrackCoverageDialog()
        {
            if (SelectedTrackCoverage is null)
                return;

            var dialog = new Views.Admin.Dialogs.PolicyFormDialog(SelectedTrackCoverage.PolicyId);
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell is not null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        private async Task CancelTrackCoverageAsync()
        {
            if (SelectedTrackCoverage is null)
                return;

            var result = MessageBox.Show(
                $"Cancel track coverage '{SelectedTrackCoverage.TrackName}'?\n\nExisting employee assignments will not be affected.",
                "Cancel Track Coverage",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var policy = await db.InsurancePolicies.FindAsync(SelectedTrackCoverage.PolicyId);
                if (policy is null)
                    return;

                policy.PolicyStatus = "Cancelled";
                policy.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();
                StatusMessage = "Track coverage cancelled.";
                await LoadAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Cancel failed: {ex.Message}";
            }
        }

        private async Task DeleteAsync()
        {
            if (SelectedFund is null)
                return;

            var confirm = MessageBox.Show(
                $"Delete source of funds '{SelectedFund.FundName}'?\n\nThis is only allowed when no beneficiary is using it.",
                "Delete Source of Funds",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var inUse =
                    await db.Beneficiaries.AnyAsync(b => b.SourceOfFunds == SelectedFund.FundName) ||
                    await db.Claims.AnyAsync(c => c.SourceOfFunds == SelectedFund.FundName) ||
                    await db.Benefits.AnyAsync(b => b.SourceOfFunds == SelectedFund.FundName);
                if (inUse)
                {
                    StatusMessage = "Cannot delete: this source is already used by employee records, claims, or benefits.";
                    return;
                }

                var fund = await db.SourceFunds.FindAsync(SelectedFund.SourceFundId);
                if (fund is not null)
                {
                    db.SourceFunds.Remove(fund);
                    await db.SaveChangesAsync();
                }

                ClearForm();
                StatusMessage = "Source deleted.";
                await LoadAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ex.Message}";
            }
        }

        private void OnTotalsChanged()
        {
            OnPropertyChanged(nameof(TotalAllocated));
            OnPropertyChanged(nameof(TotalUsed));
            OnPropertyChanged(nameof(TotalRemaining));
        }

        private static string NormalizeBarangay(string? barangay) =>
            string.IsNullOrWhiteSpace(barangay) ? "Unassigned Barangay" : barangay.Trim();

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

        private void ExportReport()
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export Source of Funds Report",
                Filter = "CSV file (*.csv)|*.csv",
                FileName = $"esurehi-source-funds-{DateTime.Today:yyyyMMdd}.csv"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                using var writer = new StreamWriter(dialog.FileName, false, Encoding.UTF8);
                writer.WriteLine("Source of Funds Summary");
                writer.WriteLine("Source Name,Type,Allocated,Used,Remaining,Beneficiaries,Status,GGMS Office Code");
                foreach (var fund in Funds)
                {
                    writer.WriteLine(string.Join(",",
                        Csv(fund.FundName),
                        Csv(fund.FundType),
                        fund.AllocatedAmount.ToString("0.00"),
                        fund.UsedAmount.ToString("0.00"),
                        fund.RemainingAmount.ToString("0.00"),
                        fund.BeneficiaryCount,
                        Csv(fund.Status),
                        Csv(fund.GgmsOfficeCode)));
                }

                writer.WriteLine();
                writer.WriteLine("Insurance Release Ledger");
                writer.WriteLine("Date,Reference,GGMS Reference,Recipient,Type,Source of Funds,Amount,Status,Details");
                foreach (var release in ReleaseLedger)
                {
                    writer.WriteLine(string.Join(",",
                        Csv(release.ReleaseDateText),
                        Csv(release.ReferenceNo),
                        Csv(release.GgmsReference),
                        Csv(release.RecipientName),
                        Csv(release.ReleaseType),
                        Csv(release.SourceOfFunds),
                        release.Amount.ToString("0.00"),
                        Csv(release.Status),
                        Csv(release.Details)));
                }

                writer.WriteLine();
                writer.WriteLine("GGMS Matching");
                writer.WriteLine("Date,Project Code,Project Name,Recipient,Type,Amount,Status,Match Status");
                foreach (var match in GgmsMatches)
                {
                    writer.WriteLine(string.Join(",",
                        Csv(match.TransactionDateText),
                        Csv(match.ProjectCode),
                        Csv(match.ProjectName),
                        Csv(match.RecipientName),
                        Csv(match.TransactionType),
                        match.Amount.ToString("0.00"),
                        Csv(match.Status),
                        Csv(match.MatchStatus)));
                }

                StatusMessage = $"Report exported: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
        }

        private static string BuildGgmsReference(int id) =>
            $"IMS-{Math.Max(0, id):000000}";

        private static string ReadString(MySqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        private static decimal ReadDecimal(MySqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0 : reader.GetDecimal(ordinal);
        }

        private static DateOnly? ReadDate(MySqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return null;

            return DateOnly.FromDateTime(reader.GetDateTime(ordinal));
        }

        private static string Csv(string? value)
        {
            var text = value ?? string.Empty;
            if (text.Contains('"'))
                text = text.Replace("\"", "\"\"");

            return text.Contains(',') || text.Contains('\n') || text.Contains('\r')
                ? $"\"{text}\""
                : text;
        }
    }
}
