using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Services;
using eSureHi.Views.Admin.UserControls;
using eSureHi.Views.Shared;

namespace eSureHi.ViewModels.Admin
{
    // ── Supporting display classes ─────────────────────────────────────
    public class ChartItem
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
        public string ColorHex { get; set; } = "#2E7D32";
        public double BarHeight { get; set; }
    }

    public class ActivityItem
    {
        public string Action { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TimeAgo { get; set; } = string.Empty;
        public string DotColor { get; set; } = "#2E7D32";
    }

    public class CalendarDay
    {
        public int Day { get; set; }
        public bool IsToday { get; set; }
        public bool HasDueDate { get; set; }
        public bool IsEmpty => Day == 0;
    }

    // ── ViewModel ──────────────────────────────────────────────────────
    public class DashboardViewModel : ObservableObject
    {
        // ── Stat Cards ─────────────────────────────────────────────────
        private int _activeEmployees;
        private int _activePolicies;
        private int _pendingClaims;
        private decimal _premiumsDue;
        private int _totalBeneficiaries;
        private int _totalBenefits;
        private int _totalUsers;
        private int _totalLogs;
        private int _totalDocuments;
        private bool _isLoading = true;

        public int ActiveEmployees
        {
            get => _activeEmployees;
            set => SetProperty(ref _activeEmployees, value);
        }
        public int ActivePolicies
        {
            get => _activePolicies;
            set => SetProperty(ref _activePolicies, value);
        }
        public int PendingClaims
        {
            get => _pendingClaims;
            set => SetProperty(ref _pendingClaims, value);
        }
        public int TotalBeneficiaries
        {
            get => _totalBeneficiaries;
            set => SetProperty(ref _totalBeneficiaries, value);
        }

        // ── User-role landing summary ──────────────────────────────────
        private int _myPendingMembers;
        public int MyPendingMembers
        {
            get => _myPendingMembers;
            set => SetProperty(ref _myPendingMembers, value);
        }

        private int _linkedActiveMembers;
        public int LinkedActiveMembers
        {
            get => _linkedActiveMembers;
            set => SetProperty(ref _linkedActiveMembers, value);
        }

        private int _myRecentClaims;
        public int MyRecentClaims
        {
            get => _myRecentClaims;
            set => SetProperty(ref _myRecentClaims, value);
        }

        private int _pendingPayments;
        public int PendingPayments
        {
            get => _pendingPayments;
            set => SetProperty(ref _pendingPayments, value);
        }

        private int _groupClaims;
        public int GroupClaims
        {
            get => _groupClaims;
            set => SetProperty(ref _groupClaims, value);
        }
        public int TotalBenefits
        {
            get => _totalBenefits;
            set => SetProperty(ref _totalBenefits, value);
        }
        public int TotalUsers
        {
            get => _totalUsers;
            set => SetProperty(ref _totalUsers, value);
        }
        public int TotalLogs
        {
            get => _totalLogs;
            set => SetProperty(ref _totalLogs, value);
        }
        public int TotalDocuments
        {
            get => _totalDocuments;
            set => SetProperty(ref _totalDocuments, value);
        }
        public decimal PremiumsDue
        {
            get => _premiumsDue;
            set
            {
                SetProperty(ref _premiumsDue, value);
                OnPropertyChanged(nameof(PremiumsDueFormatted));
            }
        }
        public string PremiumsDueFormatted
        {
            get
            {
                if (PremiumsDue >= 1_000_000) return $"₱{PremiumsDue / 1_000_000:N1}M";
                if (PremiumsDue >= 1_000) return $"₱{PremiumsDue / 1_000:N0}K";
                return $"₱{PremiumsDue:N0}";
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        // ── GGMS Fund Summary ──────────────────────────────────────────
        private decimal _ggmsAllocated;
        private decimal _ggmsSpent;
        private decimal _ggmsRemaining;
        private string _ggmsYear = DateTime.Today.Year.ToString();
        private string _ggmsStatus = "Loading...";
        private bool _ggmsLoaded;

        public decimal GgmsAllocated { get => _ggmsAllocated; set => SetProperty(ref _ggmsAllocated, value); }
        public decimal GgmsSpent { get => _ggmsSpent; set => SetProperty(ref _ggmsSpent, value); }
        public decimal GgmsRemaining { get => _ggmsRemaining; set => SetProperty(ref _ggmsRemaining, value); }
        public string GgmsYear { get => _ggmsYear; set => SetProperty(ref _ggmsYear, value); }
        public string GgmsStatus { get => _ggmsStatus; set => SetProperty(ref _ggmsStatus, value); }
        public bool GgmsLoaded { get => _ggmsLoaded; set => SetProperty(ref _ggmsLoaded, value); }

        // ── Chart Data ─────────────────────────────────────────────────
        public ObservableCollection<ChartItem> ClaimStatusItems { get; } = new();
        public ObservableCollection<ChartItem> PremiumStatusItems { get; } = new();

        private bool _hasClaimsData;
        private bool _hasPremiumData;

        public bool HasClaimsData
        {
            get => _hasClaimsData;
            set => SetProperty(ref _hasClaimsData, value);
        }
        public bool HasPremiumData
        {
            get => _hasPremiumData;
            set => SetProperty(ref _hasPremiumData, value);
        }

        // ── Recent Activity ────────────────────────────────────────────
        public ObservableCollection<ActivityItem> RecentActivityItems { get; } = new();

        private bool _hasActivity;
        public bool HasActivity
        {
            get => _hasActivity;
            set => SetProperty(ref _hasActivity, value);
        }

        private bool _isMemberManagementPopupOpen;
        public bool IsMemberManagementPopupOpen
        {
            get => _isMemberManagementPopupOpen;
            set => SetProperty(ref _isMemberManagementPopupOpen, value);
        }

        private bool _isBudgetFundsPopupOpen;
        public bool IsBudgetFundsPopupOpen
        {
            get => _isBudgetFundsPopupOpen;
            set => SetProperty(ref _isBudgetFundsPopupOpen, value);
        }

        private bool _isClaimsManagementPopupOpen;
        public bool IsClaimsManagementPopupOpen
        {
            get => _isClaimsManagementPopupOpen;
            set => SetProperty(ref _isClaimsManagementPopupOpen, value);
        }

        private bool _isPaymentManagementPopupOpen;
        public bool IsPaymentManagementPopupOpen
        {
            get => _isPaymentManagementPopupOpen;
            set => SetProperty(ref _isPaymentManagementPopupOpen, value);
        }

        private bool _isReportsPopupOpen;
        public bool IsReportsPopupOpen
        {
            get => _isReportsPopupOpen;
            set => SetProperty(ref _isReportsPopupOpen, value);
        }

        public bool CanAccessManageMembers => PermissionService.CanAccessBeneficiaries;

        // ── Calendar ───────────────────────────────────────────────────
        public ObservableCollection<CalendarDay> CalendarDays { get; } = new();

        private string _currentMonthYear = string.Empty;
        private DateTime _calendarDate = DateTime.Today;
        private readonly DispatcherTimer _clockTimer;

        public string CurrentMonthYear
        {
            get => _currentMonthYear;
            set => SetProperty(ref _currentMonthYear, value);
        }

        public string CurrentDateTimeDisplay =>
            DateTime.Now.ToString("dddd, MMMM d, yyyy   h:mm:ss tt");

        public string HeaderDateTimeDisplay =>
            DateTime.Now.ToString("ddd, MMM d yyyy  h:mm:ss tt");

        public string CurrentDateDisplay =>
            DateTime.Now.ToString("dddd, MMMM d, yyyy");

        public string CurrentTimeDisplay =>
            DateTime.Now.ToString("h:mm:ss tt");

        public string DatabaseStatusText =>
            IsUsingLocalDatabase ? "OFFLINE" : "ONLINE";

        public string DatabaseStatusColor =>
            IsUsingLocalDatabase ? "#F59E0B" : "#39D764";

        private static bool IsUsingLocalDatabase
        {
            get
            {
                var server = App.DbConfig.Server?.Trim() ?? string.Empty;
                return string.IsNullOrWhiteSpace(server) ||
                       string.Equals(server, "localhost", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(server, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(server, "::1", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(server, ".", StringComparison.OrdinalIgnoreCase);
            }
        }

        public string CurrentUserDisplay =>
            AuthService.Instance.CurrentUser?.Username ?? "Admin";

        public string WelcomeUserDisplay =>
            $"Welcome, {CurrentUserDisplay}!";

        public string CurrentRoleDisplay =>
            AuthService.Instance.CurrentUser?.Role ?? "Admin";

        public string UserInitials
        {
            get
            {
                var emp = AuthService.Instance.CurrentEmployee;
                if (emp is not null &&
                    !string.IsNullOrWhiteSpace(emp.FirstName) &&
                    !string.IsNullOrWhiteSpace(emp.LastName))
                    return $"{emp.FirstName[0]}{emp.LastName[0]}".ToUpper();

                var name = CurrentUserDisplay;
                return name.Length >= 2 ? name[..2].ToUpper() : name.ToUpper();
            }
        }

        private string _profileImagePath = ProfileImageService.GetCurrentProfileImagePath() ?? string.Empty;
        public string ProfileImagePath
        {
            get => _profileImagePath;
            set
            {
                if (SetProperty(ref _profileImagePath, value))
                    OnPropertyChanged(nameof(HasProfileImage));
            }
        }

        public bool HasProfileImage =>
            !string.IsNullOrWhiteSpace(ProfileImagePath) &&
            System.IO.File.Exists(ProfileImagePath);

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand RefreshCommand { get; }
        public RelayCommand PrevMonthCommand { get; }
        public RelayCommand NextMonthCommand { get; }
        public RelayCommand QuickDashboardCommand { get; }
        public RelayCommand QuickNewEmployeeCommand { get; }
        public RelayCommand QuickNewClaimCommand { get; }
        public RelayCommand QuickNewPolicyCommand { get; }
        public RelayCommand QuickViewReportsCommand { get; }
        public RelayCommand QuickPremiumsCommand { get; }
        public RelayCommand QuickBenefitsCommand { get; }
        public RelayCommand QuickDocumentsCommand { get; }
        public RelayCommand QuickTransactionsCommand { get; }
        public RelayCommand QuickReviewQueueCommand { get; }
        public RelayCommand QuickSourceFundsCommand { get; }
        public RelayCommand QuickBudgetFundsCommand { get; }
        public RelayCommand QuickBarangayFundsCommand { get; }
        public RelayCommand QuickGroupFundsCommand { get; }
        public RelayCommand QuickAllocatedFundsCommand { get; }
        public RelayCommand QuickCedulasCommand { get; }
        public RelayCommand QuickBeneficiariesCommand { get; }
        public RelayCommand QuickRegisterMemberCommand { get; }
        public RelayCommand QuickManageMembersCommand { get; }
        public RelayCommand QuickCompanyProfileCommand { get; }
        public RelayCommand QuickSettingsCommand { get; }
        public RelayCommand QuickFileClaimCommand { get; }
        public RelayCommand QuickGroupClaimCommand { get; }
        public RelayCommand QuickPendingClaimsCommand { get; }
        public RelayCommand QuickAllClaimsCommand { get; }
        public RelayCommand QuickAdvancePaymentCommand { get; }
        public RelayCommand QuickPendingPaymentsCommand { get; }
        public RelayCommand QuickPaymentLedgerCommand { get; }
        public RelayCommand QuickMonthlyPaymentReportCommand { get; }
        public RelayCommand QuickMonthlyClaimsReportCommand { get; }
        public RelayCommand QuickMemberReportsCommand { get; }
        public RelayCommand QuickAnnouncementsCommand { get; }
        public RelayCommand QuickManageAccountsCommand { get; }
        public RelayCommand ToggleMemberManagementCommand { get; }
        public RelayCommand ToggleClaimsManagementCommand { get; }
        public RelayCommand TogglePaymentManagementCommand { get; }
        public RelayCommand ToggleReportsCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }
        public RelayCommand LogoutCommand { get; }
        public RelayCommand ChangeProfileImageCommand { get; }

        public bool IsEmployee => AuthService.Instance.IsEmployee;
        public bool IsUserRegistrar => PermissionService.IsUserRegistrar(AuthService.Instance.CurrentUser?.Role);
        public bool ShowUserLandingDashboard => !IsEmployee && IsUserRegistrar;
        public bool ShowAdminLandingDashboard => !IsEmployee && !IsUserRegistrar;
        public bool ShowEmployeesTile => PermissionService.CanAccessEmployees;
        public bool ShowBeneficiariesTile =>
            !PermissionService.IsAdminReviewer(AuthService.Instance.CurrentUser?.Role) &&
            PermissionService.CanAccessBeneficiaries;
        public bool ShowPoliciesTile => AuthService.Instance.IsEmployee && PermissionService.CanAccessMyPolicies;
        public bool ShowClaimsTile =>
            !PermissionService.IsAdminReviewer(AuthService.Instance.CurrentUser?.Role) &&
            (AuthService.Instance.IsEmployee ? PermissionService.CanAccessMyClaims : PermissionService.CanAccessClaims);
        public bool ShowPremiumsTile => AuthService.Instance.IsEmployee ? PermissionService.CanAccessMyPremiums : PermissionService.CanAccessPremiums;
        public bool ShowReportsTile => PermissionService.CanAccessReports;
        public bool ShowSourceFundsTile => PermissionService.CanAccessSourceFunds;
        public bool ShowCedulasTile => PermissionService.CanAccessCedulas;
        public bool ShowBenefitsTile => AuthService.Instance.IsEmployee ? PermissionService.CanAccessMyBenefits : PermissionService.CanAccessBenefits;
        public bool ShowDocumentsTile => PermissionService.CanAccessDocuments;
        public bool ShowTransactionsTile => PermissionService.CanAccessTransactions;
        public bool ShowReviewQueueTile => PermissionService.CanAccessReviewQueue;
        public bool ShowDashboardTile => PermissionService.CanAccessDashboard;
        public bool ShowSettingsTile => PermissionService.CanAccessSettings;

        // ── Constructor ────────────────────────────────────────────────
        public DashboardViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            BackToDashboardCommand = new RelayCommand(() =>
                NavigationService.Instance.NavigateTo(new HomeView()));

            PrevMonthCommand = new RelayCommand(() =>
            {
                _calendarDate = _calendarDate.AddMonths(-1);
                BuildCalendar();
            });
            NextMonthCommand = new RelayCommand(() =>
            {
                _calendarDate = _calendarDate.AddMonths(1);
                BuildCalendar();
            });

            QuickDashboardCommand = new RelayCommand(() =>
                OpenPage("Dashboard", () => new DashboardView()));
            
            QuickNewEmployeeCommand = new RelayCommand(() =>
                OpenPage("Employees", () => new EmployeesView()));
            
            QuickNewClaimCommand = new RelayCommand(() =>
            {
                if (AuthService.Instance.IsEmployee)
                    OpenPage("My Claims", () => new MyClaimsView());
                else
                    OpenPage("Claims", () => new ClaimsView(ClaimsListMode.All));
            });

            QuickNewPolicyCommand = new RelayCommand(() =>
            {
                if (AuthService.Instance.IsEmployee)
                    OpenPage("My Policies", () => new MyPoliciesView());
                else
                    OpenPage("Source of Funds", () => new SourceFundsView());
            });

            QuickViewReportsCommand = new RelayCommand(() =>
                OpenPage("Reports", () => new ReportsView()));

            QuickPremiumsCommand = new RelayCommand(() =>
            {
                if (AuthService.Instance.IsEmployee)
                    OpenPage("My Premiums", () => new MyPremiumsView());
                else
                    OpenPage("Premiums", () => new PremiumsView());
            });

            QuickBenefitsCommand = new RelayCommand(() =>
            {
                if (AuthService.Instance.IsEmployee)
                    OpenPage("My Benefits", () => new MyBenefitsView());
                else
                    OpenPage("Benefits", () => new BenefitsView());
            });

            QuickDocumentsCommand = new RelayCommand(() =>
                OpenPage("Documents", () => new DocumentsView()));

            QuickTransactionsCommand = new RelayCommand(() =>
                OpenPage("Transactions", () => new TransactionsView()));

            QuickReviewQueueCommand = new RelayCommand(() =>
                OpenPage("Review Queue", OpenReviewQueue));

            QuickSourceFundsCommand = new RelayCommand(() =>
                OpenPage("Source of Funds", () => new SourceFundsView()));

            QuickBudgetFundsCommand = new RelayCommand(() =>
            {
                IsBudgetFundsPopupOpen = !IsBudgetFundsPopupOpen;
            });

            QuickBarangayFundsCommand = new RelayCommand(() =>
            {
                IsBudgetFundsPopupOpen = false;
                OpenPage("Distribution Management", () => new eSureHi.Views.Admin.UserControls.DistributionBatchView());
            });

            QuickGroupFundsCommand = new RelayCommand(() =>
            {
                IsBudgetFundsPopupOpen = false;
                OpenPage("Source of Funds", () => new SourceFundsView(SourceFundsLandingMode.BarangayFunds));
            });

            QuickAllocatedFundsCommand = new RelayCommand(() =>
            {
                IsBudgetFundsPopupOpen = false;
                OpenPage("Source of Funds", () => new SourceFundsView(SourceFundsLandingMode.AllocatedFunds));
            });

            QuickCedulasCommand = new RelayCommand(() => OpenPage("Cedulas", () => new CedulaManagementView()));
            
            QuickBeneficiariesCommand = new RelayCommand(() =>
{
    IsMemberManagementPopupOpen = false;
    OpenPage("Register Member", () => new BeneficiariesView(null, openInitialSearch: false));
});

            QuickRegisterMemberCommand = new RelayCommand(() =>
            {
                IsMemberManagementPopupOpen = false;
                OpenPage("Register Member", () => new BeneficiariesView(null, openInitialSearch: false));
            });

            QuickManageMembersCommand = new RelayCommand(() =>
            {
                IsMemberManagementPopupOpen = false;
                OpenPage("Beneficiaries", () => new BeneficiariesView());
            });

            QuickCompanyProfileCommand = new RelayCommand(() => OpenPage("Company Profile", () => new CompanyProfileView()));

            QuickSettingsCommand = new RelayCommand(() =>
                OpenPage("Settings", () => new SettingsView()));

            // Claims Management popup (User landing + Admin)
            QuickFileClaimCommand = new RelayCommand(() =>
            {
                IsClaimsManagementPopupOpen = false;
                if (!PermissionService.CanAccessPage("File a Claim"))
                {
                    ShowAccessDenied("File a Claim");
                    return;
                }
                var dialog = AuthService.Instance.IsBeneficiary && AuthService.Instance.CurrentUser?.BenId is int benId
                    ? new Views.Admin.Dialogs.ClaimFormDialog(benId, beneficiaryMode: true)
                    : new Views.Admin.Dialogs.ClaimFormDialog();

                dialog.SetSaveCallback(async () =>
                {
                    if (NavigationService.Instance.CurrentPage is ClaimsView claimsView && claimsView.DataContext is ClaimsViewModel claimsVm)
                    {
                        await claimsVm.LoadAsync();
                    }
                });

                if (App.ActiveShell is not null && App.ActiveShell != dialog)
                    dialog.Owner = App.ActiveShell;
                dialog.ShowDialog();
            });
            QuickGroupClaimCommand = new RelayCommand(() =>
            {
                IsClaimsManagementPopupOpen = false;
                OpenPage("Group Claim", () => new ClaimsView(ClaimsListMode.GroupClaim));
            });
            QuickPendingClaimsCommand = new RelayCommand(() =>
            {
                IsClaimsManagementPopupOpen = false;
                OpenPage("Pending Claims", () => new ClaimsView(ClaimsListMode.PendingOnly));
            });
            QuickAllClaimsCommand = new RelayCommand(() =>
            {
                IsClaimsManagementPopupOpen = false;
                OpenPage("All Claims", () => new ClaimsView(ClaimsListMode.All));
            });

            // Payment Management popup
            QuickAdvancePaymentCommand = new RelayCommand(() =>
            {
                IsPaymentManagementPopupOpen = false;
                OpenPage("Advance Payment", () => new AdvancePaymentView());
            });
            QuickPendingPaymentsCommand = new RelayCommand(() =>
            {
                IsPaymentManagementPopupOpen = false;
                OpenPage("Pending Payment", () => new PaymentsView(PaymentsListMode.PendingOnly));
            });
            QuickPaymentLedgerCommand = new RelayCommand(() =>
            {
                IsPaymentManagementPopupOpen = false;
                OpenPage("All Payment Ledger", () => new PaymentsView(PaymentsListMode.All));
            });

            // Generate Reports popup — each opens the Reports page scoped to its own tab
            QuickMonthlyPaymentReportCommand = new RelayCommand(() =>
            {
                IsReportsPopupOpen = false;
                OpenPage("Reports", () => new ReportsView(4));
            });
            QuickMonthlyClaimsReportCommand = new RelayCommand(() =>
            {
                IsReportsPopupOpen = false;
                OpenPage("Reports", () => new ReportsView(5));
            });
            QuickMemberReportsCommand = new RelayCommand(() =>
            {
                IsReportsPopupOpen = false;
                OpenPage("Reports", () => new ReportsView(6));
            });

            // Admin landing tiles
            QuickAnnouncementsCommand = new RelayCommand(() =>
                OpenPage("Announcements", () => new AnnouncementsView()));
            QuickManageAccountsCommand = new RelayCommand(() =>
                OpenPage("Manage Accounts", () => new ManageAccountsView()));

            // User-landing tile toggles — open one sub-menu popup, close the others.
            ToggleMemberManagementCommand = new RelayCommand(() =>
            {
                bool open = !IsMemberManagementPopupOpen;
                CloseLandingPopups();
                IsMemberManagementPopupOpen = open;
            });
            ToggleClaimsManagementCommand = new RelayCommand(() =>
            {
                bool open = !IsClaimsManagementPopupOpen;
                CloseLandingPopups();
                IsClaimsManagementPopupOpen = open;
            });
            TogglePaymentManagementCommand = new RelayCommand(() =>
            {
                bool open = !IsPaymentManagementPopupOpen;
                CloseLandingPopups();
                IsPaymentManagementPopupOpen = open;
            });
            ToggleReportsCommand = new RelayCommand(() =>
            {
                bool open = !IsReportsPopupOpen;
                CloseLandingPopups();
                IsReportsPopupOpen = open;
            });

            LogoutCommand = new RelayCommand(Logout);
            ChangeProfileImageCommand = new RelayCommand(ChangeProfileImage);

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, _) =>
            {
                OnPropertyChanged(nameof(CurrentDateTimeDisplay));
                OnPropertyChanged(nameof(HeaderDateTimeDisplay));
                OnPropertyChanged(nameof(CurrentDateDisplay));
                OnPropertyChanged(nameof(CurrentTimeDisplay));
            };
            _clockTimer.Start();

            BuildCalendar();
            _ = LoadAsync();
        }

        // ── Load ───────────────────────────────────────────────────────
        private async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                // ── Stat Cards ─────────────────────────────────────────
                ActiveEmployees = await db.Employees
                    .CountAsync(e => e.EmploymentStatus == "Active");

                ActivePolicies = await db.InsurancePolicies
                    .CountAsync(p => p.PolicyStatus == "Active");

                PendingClaims = await db.Claims
                    .CountAsync(c => c.ClaimStatus == "Submitted" ||
                                     c.ClaimStatus == "Under Review");

                TotalBeneficiaries = await db.Beneficiaries.CountAsync();

                // User-landing summary. NOTE: Beneficiary has no "registered by" column,
                // so pending is system-wide (WorkflowStatus == Pending), not per-user.
                // Add a RegisteredBy column later if true per-user attribution is required.
                MyPendingMembers = await db.Beneficiaries
                    .CountAsync(b => b.WorkflowStatus == "Pending");
                LinkedActiveMembers = await db.Beneficiaries
                    .CountAsync(b => b.IsActive && b.WorkflowStatus != "Pending");
                MyRecentClaims = await db.Claims
                    .CountAsync(c => c.ClaimStatus != "Draft");

                // Same system-wide caveat as above: these back the user-landing stat
                // cards and are not scoped per-user until attribution columns exist.
                PendingPayments = await db.Payments
                    .CountAsync(p => p.Status == "Pending");
                GroupClaims = await db.Claims
                    .CountAsync(c => c.SourceOfFunds != null && c.SourceOfFunds != "");
                TotalBenefits = await db.Benefits.CountAsync();
                TotalUsers = await db.SystemUsers.CountAsync();
                TotalLogs = await db.AuditLogs.CountAsync();
                TotalDocuments = await db.Documents.CountAsync();

                var today = DateOnly.FromDateTime(DateTime.Today);
                var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
                var lastOfMonth = firstOfMonth.AddMonths(1).AddDays(-1);

                PremiumsDue = await db.Premiums
                    .Where(p => p.DueDate >= firstOfMonth &&
                                p.DueDate <= lastOfMonth &&
                                p.PaymentStatus == "Unpaid")
                    .SumAsync(p => (decimal?)p.TotalAmount) ?? 0;

                // ── Claims by Status chart ─────────────────────────────
                var claimsData = await db.Claims
                    .GroupBy(c => c.ClaimStatus)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync();

                ClaimStatusItems.Clear();
                var claimColors = new Dictionary<string, string>
                {
                    { "Draft",              "#9E9E9E" },
                    { "Submitted",          "#2E7D32" },
                    { "Under Review",       "#F57F17" },
                    { "Approved",           "#2E7D32" },
                    { "Partially Approved", "#558B2F" },
                    { "Rejected",           "#C62828" },
                    { "Released",           "#00838F" },
                    { "Cancelled",          "#6D4C41" }
                };

                int maxClaims = claimsData.Any() ? claimsData.Max(x => x.Count) : 1;
                foreach (var item in claimsData)
                {
                    ClaimStatusItems.Add(new ChartItem
                    {
                        Label = item.Status,
                        Count = item.Count,
                        ColorHex = claimColors.TryGetValue(item.Status, out var cc)
                                        ? cc : "#2E7D32",
                        BarHeight = Math.Max(6,
                                        item.Count / (double)maxClaims * 120)
                    });
                }
                HasClaimsData = ClaimStatusItems.Any();

                // ── Premium Status chart ───────────────────────────────
                var premiumData = await db.Premiums
                    .GroupBy(p => p.PaymentStatus)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .ToListAsync();

                PremiumStatusItems.Clear();
                var premiumColors = new Dictionary<string, string>
                {
                    { "Paid",    "#2E7D32" },
                    { "Unpaid",  "#C62828" },
                    { "Partial", "#F57F17" },
                    { "Late",    "#E65100" },
                    { "Waived",  "#2E7D32" }
                };

                int maxPremiums = premiumData.Any() ? premiumData.Max(x => x.Count) : 1;
                foreach (var item in premiumData)
                {
                    PremiumStatusItems.Add(new ChartItem
                    {
                        Label = item.Status,
                        Count = item.Count,
                        ColorHex = premiumColors.TryGetValue(item.Status, out var pc)
                                        ? pc : "#2E7D32",
                        BarHeight = Math.Max(6,
                                        item.Count / (double)maxPremiums * 120)
                    });
                }
                HasPremiumData = PremiumStatusItems.Any();

                // ── Recent Activity ────────────────────────────────────
                var logs = await db.AuditLogs
                    .OrderByDescending(l => l.LoggedAt)
                    .Take(5)
                    .ToListAsync();

                RecentActivityItems.Clear();
                var actionColors = new Dictionary<string, string>
                {
                    { "INSERT", "#2E7D32" },
                    { "UPDATE", "#2E7D32" },
                    { "DELETE", "#C62828" },
                    { "LOGIN",  "#F57F17" }
                };

                foreach (var log in logs)
                {
                    RecentActivityItems.Add(new ActivityItem
                    {
                        Action = log.Action,
                        Description = ParseAuditDescription(log.NewValues)
                            ?? $"{log.TableName} #{log.RecordId}",
                        TimeAgo = GetTimeAgo(log.LoggedAt),
                        DotColor = actionColors.TryGetValue(
                                          log.Action.ToUpper(), out var ac)
                                          ? ac : "#9E9E9E"
                    });
                }
                HasActivity = RecentActivityItems.Any();
            }
            catch
            {
                // Silent fail — zeros remain, empty states show
            }
            finally
            {
                IsLoading = false;
                _ = LoadGgmsFundAsync();
            }
        }

        // ── Calendar Builder ───────────────────────────────────────────
        private void BuildCalendar()
        {
            CurrentMonthYear = _calendarDate.ToString("MMMM yyyy");
            CalendarDays.Clear();

            var firstDay = new DateTime(_calendarDate.Year, _calendarDate.Month, 1);
            int startOffset = (int)firstDay.DayOfWeek; // Sunday = 0

            for (int i = 0; i < startOffset; i++)
                CalendarDays.Add(new CalendarDay { Day = 0 });

            int daysInMonth = DateTime.DaysInMonth(_calendarDate.Year, _calendarDate.Month);
            var today = DateTime.Today;

            for (int d = 1; d <= daysInMonth; d++)
            {
                CalendarDays.Add(new CalendarDay
                {
                    Day = d,
                    IsToday = _calendarDate.Year == today.Year &&
                              _calendarDate.Month == today.Month &&
                              d == today.Day
                });
            }
        }

        // ── Helpers ────────────────────────────────────────────────────
        private static string GetTimeAgo(DateTime dt)
        {
            var diff = DateTime.Now - dt;
            if (diff.TotalMinutes < 1) return "Just now";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} hr ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} days ago";
            return dt.ToString("MMM d");
        }

        // ── GGMS Fund Summary ──────────────────────────────────────────
        private async Task LoadGgmsFundAsync()
        {
            try
            {
                var summary = await Services.GgmsService.GetFundSummaryAsync();
                if (summary.IsLoaded)
                {
                    GgmsAllocated = summary.AllocatedAmount;
                    GgmsSpent = summary.SpentAmount;
                    GgmsRemaining = summary.RemainingAmount;
                    GgmsYear = summary.Year.ToString();
                    GgmsStatus = string.Empty;
                    GgmsLoaded = true;
                }
                else
                {
                    GgmsStatus = summary.ErrorMessage;
                    GgmsLoaded = false;
                }
            }
            catch (Exception ex)
            {
                GgmsStatus = $"GGMS unavailable: {ex.Message}";
                GgmsLoaded = false;
            }
        }

        private static string? ParseAuditDescription(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                return doc.RootElement
                          .GetProperty("description")
                          .GetString();
            }
            catch { return json; }
        }

        private void Logout()
        {
            var result = MessageBox.Show(
                "Log out of the current account?",
                "Confirm Logout",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            AuthService.Instance.Logout();

            var loginWindow = new LoginWindow();
            loginWindow.Show();

            App.ActiveShell?.Close();
        }

        private void ChangeProfileImage()
        {
            var path = ProfileImageService.PickAndSaveCurrentProfileImage();
            if (!string.IsNullOrWhiteSpace(path))
                ProfileImagePath = path;
        }

        private void CloseLandingPopups()
        {
            IsMemberManagementPopupOpen = false;
            IsClaimsManagementPopupOpen = false;
            IsPaymentManagementPopupOpen = false;
            IsReportsPopupOpen = false;
            IsBudgetFundsPopupOpen = false;
        }

        private static void OpenPage(string page, Func<System.Windows.Controls.UserControl> createView)
        {
            if (!PermissionService.CanAccessPage(page))
            {
                ShowAccessDenied(page);
                return;
            }

            NavigationService.Instance.NavigateTo(createView());
        }

        private static void OpenPage(string page, Action openAction)
        {
            if (!PermissionService.CanAccessPage(page))
            {
                ShowAccessDenied(page);
                return;
            }

            openAction();
        }

        private static void ShowAccessDenied(string page)
        {
            var role = AuthService.Instance.CurrentUser?.Role ?? "User";
            var message = AuthService.Instance.IsEmployee
                ? "Access denied. This module is for administrators only."
                : $"Cannot open {page} because your {role} account does not have permission for this page.";

            var dialog = new SystemNotificationDialog("Access Restricted", message);
            if (App.ActiveShell is not null)
                dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        private void OpenBeneficiaryImport()
        {
            var dialog = new Views.Admin.Dialogs.BeneficiaryStagingDialog();
            if (App.ActiveShell != null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        private void OpenTransactionQueue()
        {
            var dialog = new Views.Admin.Dialogs.TransactionQueueDialog();
            if (App.ActiveShell != null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        private void OpenClaimQueue()
        {
            var dialog = new Views.Admin.Dialogs.ClaimQueueDialog();
            if (App.ActiveShell != null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        private void OpenReviewQueue()
        {
            var dialog = new Views.Admin.Dialogs.ClaimQueueDialog(openSelectionInReviewPage: true);
            if (App.ActiveShell != null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }
    }
}
