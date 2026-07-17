using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using eSureHi.Views.Admin.UserControls;
using eSureHi.Views.Shared;

namespace eSureHi.ViewModels.Admin
{
    public class AdminShellViewModel : ObservableObject
    {
        // ── Page Title ─────────────────────────────────────────────────
        private string _currentPageTitle = "Home";
        public string CurrentPageTitle
        {
            get => _currentPageTitle;
            set => SetProperty(ref _currentPageTitle, value);
        }

        private bool _isSidebarVisible;
        public bool IsSidebarVisible
        {
            get => _isSidebarVisible;
            set
            {
                if (SetProperty(ref _isSidebarVisible, value))
                    SidebarWidth = value ? new GridLength(248) : new GridLength(0);
            }
        }

        private GridLength _sidebarWidth = new(0);
        public GridLength SidebarWidth
        {
            get => _sidebarWidth;
            set => SetProperty(ref _sidebarWidth, value);
        }

        // ── User Info ──────────────────────────────────────────────────
        public string CurrentUsername =>
            AuthService.Instance.CurrentUser?.Username ?? "Admin";

        public string CurrentRole =>
            AuthService.Instance.CurrentUser?.Role ?? "Admin";

        public bool IsEmployee => AuthService.Instance.IsEmployee;
        public bool IsBeneficiary => AuthService.Instance.IsBeneficiary;
        public bool IsAdminReviewer => PermissionService.IsAdminReviewer(CurrentRole);
        public bool IsUserRegistrar => PermissionService.IsUserRegistrar(CurrentRole);
        public bool IsAdminNavigationFlow => !IsEmployee && !IsBeneficiary && !IsUserRegistrar;
        public bool ShowClassicNavigationFlow => !IsAdminNavigationFlow;

        public string UserInitials
        {
            get
            {
                var emp = AuthService.Instance.CurrentEmployee;
                if (emp is not null)
                    return $"{emp.FirstName[0]}{emp.LastName[0]}".ToUpper();
                var name = CurrentUsername;
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

        // ── Notifications ──────────────────────────────────────────────
        private int _unreadNotifications;
        public int UnreadNotifications
        {
            get => _unreadNotifications;
            set => SetProperty(ref _unreadNotifications, value);
        }

        private string _syncStatusText = OfflineOnlineSyncService.CurrentStatus.Message;
        public string SyncStatusText
        {
            get => _syncStatusText;
            set => SetProperty(ref _syncStatusText, value);
        }

        private string _syncStatusColor = OfflineOnlineSyncService.CurrentStatus.Color;
        public string SyncStatusColor
        {
            get => _syncStatusColor;
            set => SetProperty(ref _syncStatusColor, value);
        }

        private string _crossSystemStatusText = OfflineOnlineSyncService.CurrentStatus.CrossSystemMessage;
        public string CrossSystemStatusText
        {
            get => _crossSystemStatusText;
            set => SetProperty(ref _crossSystemStatusText, value);
        }

        // ── Notifications Panel ────────────────────────────────────────
        private NotificationsViewModel? _notificationsVm;
        public NotificationsViewModel NotificationsVm
        {
            get
            {
                if (_notificationsVm is null)
                {
                    _notificationsVm = new NotificationsViewModel();
                    _notificationsVm.OnUnreadCountChanged = count =>
                        UnreadNotifications = count;
                }
                return _notificationsVm;
            }
        }

        private bool _isNotificationsPanelOpen;
        public bool IsNotificationsPanelOpen
        {
            get => _isNotificationsPanelOpen;
            set => SetProperty(ref _isNotificationsPanelOpen, value);
        }

        private bool _isMemberManagementPopupOpen;
        public bool IsMemberManagementPopupOpen
        {
            get => _isMemberManagementPopupOpen;
            set => SetProperty(ref _isMemberManagementPopupOpen, value);
        }

        private bool _isClaimsMenuPopupOpen;
        public bool IsClaimsMenuPopupOpen
        {
            get => _isClaimsMenuPopupOpen;
            set => SetProperty(ref _isClaimsMenuPopupOpen, value);
        }

        private bool _isBudgetFundsPopupOpen;
        public bool IsBudgetFundsPopupOpen
        {
            get => _isBudgetFundsPopupOpen;
            set => SetProperty(ref _isBudgetFundsPopupOpen, value);
        }

        private bool _isAdminToolsPopupOpen;
        public bool IsAdminToolsPopupOpen
        {
            get => _isAdminToolsPopupOpen;
            set => SetProperty(ref _isAdminToolsPopupOpen, value);
        }

        public RelayCommand ToggleNotificationsCommand { get; private set; } = null!;
        public RelayCommand ToggleSidebarCommand { get; }
        public RelayCommand ChangeProfileImageCommand { get; }
        public RelayCommand SyncNowCommand { get; }
        private void InitNotifications()
        {
            ToggleNotificationsCommand = new RelayCommand(() =>
            {
                NavigateTo("Home");
                IsNotificationsPanelOpen = true;
                _ = NotificationsVm.LoadAsync();
            });

            // Load initial count
            _ = LoadNotificationCountAsync();
        }

        private async Task LoadNotificationCountAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var userId = AuthService.Instance.CurrentUser?.UserId ?? 0;
                UnreadNotifications = await db.Notifications
                    .CountAsync(n => n.RecipientId == userId && !n.IsRead);
            }
            catch
            {
                UnreadNotifications = 0;
            }
        }

        // ── Logout ─────────────────────────────────────────────────────
        public async Task ShowLoginNotificationAsync()
        {
            NavigateTo("Home");
            await NotificationsVm.LoadAsync();
            IsNotificationsPanelOpen = false;
        }

        public Action? LogoutAction { get; set; }
        public RelayCommand LogoutCommand { get; }

        // ── Nav Commands ───────────────────────────────────────────────
        public RelayCommand NavHomeCommand { get; }
        public RelayCommand NavDashboardCommand { get; }
        public RelayCommand NavReviewQueueCommand { get; }
        public RelayCommand NavMyProfileCommand { get; }
        public RelayCommand NavMyPoliciesCommand { get; }
        public RelayCommand NavMyClaimsCommand { get; }
        public RelayCommand NavMyPremiumsCommand { get; }
        public RelayCommand NavMyBenefitsCommand { get; }
        public RelayCommand NavEmployeesCommand { get; }
        public RelayCommand NavBeneficiariesCommand { get; }
        public RelayCommand OpenMemberManagementCommand { get; }
        public RelayCommand OpenClaimsMenuCommand { get; }
        public RelayCommand OpenBudgetFundsCommand { get; }
        public RelayCommand OpenAdminToolsCommand { get; }
        public RelayCommand NavRegisterMemberCommand { get; }
        public RelayCommand NavManageMembersCommand { get; }
        public RelayCommand NavPoliciesCommand { get; }
        public RelayCommand NavClaimsCommand { get; }
        public RelayCommand NavAllClaimsCommand { get; }
        public RelayCommand NavPremiumsCommand { get; }
        public RelayCommand NavBenefitsCommand { get; }
        public RelayCommand NavDocumentsCommand { get; }
        public RelayCommand NavTransactionsCommand { get; }
        public RelayCommand NavSourceFundsCommand { get; }
        public RelayCommand NavBarangayFundsCommand { get; }
        public RelayCommand NavGroupFundsCommand { get; }
        public RelayCommand NavAllocatedFundsCommand { get; }
        public RelayCommand NavPendingMembersCommand { get; }
        public RelayCommand NavPendingClaimsCommand { get; }
        public RelayCommand NavCedulasCommand { get; }
        public RelayCommand NavReportsCommand { get; }
        public RelayCommand NavCompanyProfileCommand { get; }
        public RelayCommand NavSettingsCommand { get; }
        public RelayCommand NavBeneficiaryPortalCommand { get; }
        public RelayCommand NavManageAccountsCommand { get; }
        public RelayCommand NavAnnouncementsCommand { get; }

        public bool CanAccessHome => PermissionService.CanAccessHome;
        public bool CanAccessDashboard => PermissionService.CanAccessDashboard;
        public bool CanAccessMyProfile => PermissionService.CanAccessMyProfile;
        public bool CanAccessMyPolicies => PermissionService.CanAccessMyPolicies;
        public bool CanAccessMyClaims => PermissionService.CanAccessMyClaims;
        public bool CanAccessMyPremiums => PermissionService.CanAccessMyPremiums;
        public bool CanAccessMyBenefits => PermissionService.CanAccessMyBenefits;
        public bool CanAccessReviewQueue => PermissionService.CanAccessReviewQueue;
        public bool CanAccessPendingMembers => PermissionService.CanAccessPendingMembers;
        public bool CanAccessPendingClaims => PermissionService.CanAccessPendingClaims;
        public bool CanAccessUserMemberFlow => PermissionService.CanAccessUserMemberFlow;
        public bool CanAccessRegisterMember => PermissionService.CanAccessRegisterMember;
        public bool CanAccessManageMembers => PermissionService.CanAccessManageMembers;
        public bool CanAccessManageAccounts => PermissionService.CanAccessPage("Manage Accounts");
        public bool CanAccessAnnouncements => PermissionService.CanAccessPage("Announcements");
        public bool ShowMemberManagementMenu => CanAccessRegisterMember || CanAccessManageMembers;
        public bool ShowUserMemberFlow => !IsAdminReviewer && CanAccessUserMemberFlow;
        public bool ShowClaimManagementModule => !IsAdminReviewer && CanAccessClaims;
        public bool ShowAdminReviewFlow => IsAdminReviewer;
        public bool ShowReviewQueueModule => !IsAdminReviewer && !IsUserRegistrar && CanAccessReviewQueue;
        public bool ShowMemberManagementSection => ShowMemberManagementMenu || CanAccessEmployees;
        public bool ShowPaymentManagementSection => CanAccessPremiums || CanAccessSourceFunds;
        public bool ShowClaimManagementSection => ShowClaimManagementModule || ShowAdminReviewFlow || ShowReviewQueueModule || CanAccessBenefits;
        public bool ShowReportingSection => CanAccessTransactions || CanAccessDocuments || CanAccessCedulas || CanAccessReports;
        public bool ShowSetupSection => CanAccessPolicies || CanAccessCompanyProfile || CanAccessSettings;
        public bool CanAccessEmployees => PermissionService.CanAccessEmployees;
        public bool CanAccessBeneficiaries => PermissionService.CanAccessBeneficiaries;
        public bool CanAccessPolicies => PermissionService.CanAccessPolicies;
        public bool CanAccessClaims => PermissionService.CanAccessClaims;
        public bool CanAccessPremiums => PermissionService.CanAccessPremiums;
        public bool CanAccessBenefits => PermissionService.CanAccessBenefits;
        public bool CanAccessDocuments => PermissionService.CanAccessDocuments;
        public bool CanAccessTransactions => PermissionService.CanAccessTransactions;
        public bool CanAccessSourceFunds => PermissionService.CanAccessSourceFunds;
        public bool CanAccessCedulas => PermissionService.CanAccessCedulas;
        public bool CanAccessReports => PermissionService.CanAccessReports;
        public bool CanAccessCompanyProfile => PermissionService.CanAccessCompanyProfile;
        public bool CanAccessSettings => PermissionService.CanAccessSettings;

        // ── Constructor ────────────────────────────────────────────────
        public AdminShellViewModel()
        {
            LogoutCommand = new RelayCommand(() => LogoutAction?.Invoke());
            ChangeProfileImageCommand = new RelayCommand(ChangeProfileImage);
            SyncNowCommand = new RelayCommand(async () => await RunManualSyncAsync());
            ToggleSidebarCommand = new RelayCommand(() =>
                IsSidebarVisible = !IsSidebarVisible);
            InitNotifications();

            NavHomeCommand = new RelayCommand(() => NavigateFromSidebar("Home"));
            NavDashboardCommand = new RelayCommand(() => NavigateFromSidebar("Dashboard"));
            NavReviewQueueCommand = new RelayCommand(() => NavigateFromSidebar("Review Queue"));
            NavMyProfileCommand = new RelayCommand(() => NavigateFromSidebar("My Profile"));
            NavMyPoliciesCommand = new RelayCommand(() => NavigateFromSidebar("My Policies"));
            NavMyClaimsCommand = new RelayCommand(() => NavigateFromSidebar("My Claims"));
            NavMyPremiumsCommand = new RelayCommand(() => NavigateFromSidebar("My Premiums"));
            NavMyBenefitsCommand = new RelayCommand(() => NavigateFromSidebar("My Benefits"));
            NavEmployeesCommand = new RelayCommand(() => NavigateFromSidebar("Employees"));
            NavBeneficiariesCommand = new RelayCommand(() => NavigateFromSidebar("Register Member"));
            OpenMemberManagementCommand = new RelayCommand(() =>
            {
                IsMemberManagementPopupOpen = !IsMemberManagementPopupOpen;
            });
            OpenClaimsMenuCommand = new RelayCommand(() =>
            {
                IsClaimsMenuPopupOpen = !IsClaimsMenuPopupOpen;
            });
            OpenBudgetFundsCommand = new RelayCommand(() =>
            {
                IsBudgetFundsPopupOpen = !IsBudgetFundsPopupOpen;
            });
            OpenAdminToolsCommand = new RelayCommand(() =>
            {
                IsAdminToolsPopupOpen = !IsAdminToolsPopupOpen;
            });
            NavRegisterMemberCommand = new RelayCommand(() => NavigateFromSidebar("Register Member"));
            NavManageMembersCommand = new RelayCommand(() => NavigateFromSidebar("Manage Members"));
            NavPoliciesCommand = new RelayCommand(() => NavigateFromSidebar("Policies"));
            NavClaimsCommand = new RelayCommand(() => NavigateFromSidebar("Claims"));
            NavAllClaimsCommand = new RelayCommand(() => NavigateFromSidebar("All Claims"));
            NavPremiumsCommand = new RelayCommand(() => NavigateFromSidebar("Premiums"));
            NavBenefitsCommand = new RelayCommand(() => NavigateFromSidebar("Benefits"));
            NavDocumentsCommand = new RelayCommand(() => NavigateFromSidebar("Documents"));
            NavTransactionsCommand = new RelayCommand(() => NavigateFromSidebar("Transactions"));
            NavSourceFundsCommand = new RelayCommand(() => NavigateFromSidebar("Source of Funds"));
            NavBarangayFundsCommand = new RelayCommand(() => NavigateFromSidebar("Barangay Funds"));
            NavGroupFundsCommand = new RelayCommand(() => NavigateFromSidebar("Group Funds"));
            NavAllocatedFundsCommand = new RelayCommand(() => NavigateFromSidebar("Allocated Funds"));
            NavPendingMembersCommand = new RelayCommand(() => OpenRestrictedDialog("Pending Members", OpenBeneficiaryQueue));
            NavPendingClaimsCommand = new RelayCommand(() => NavigateFromSidebar("Pending Claims"));
            NavCedulasCommand = new RelayCommand(() => NavigateFromSidebar("Cedulas"));
            NavReportsCommand = new RelayCommand(() => NavigateFromSidebar("Reports"));
            NavCompanyProfileCommand = new RelayCommand(() => NavigateFromSidebar("Company Profile"));
            NavSettingsCommand = new RelayCommand(() => NavigateFromSidebar("Settings"));
            NavBeneficiaryPortalCommand = new RelayCommand(() => NavigateFromSidebar("Beneficiary Portal"));
            NavManageAccountsCommand = new RelayCommand(() => NavigateFromSidebar("Manage Accounts"));
            NavAnnouncementsCommand = new RelayCommand(() => NavigateFromSidebar("Announcements"));

            // Seed test notifications after login so correct user_id is used
            _ = eSureHi.ViewModels.Admin.NotificationsViewModel.SeedTestNotificationsAsync();
            OfflineOnlineSyncService.StatusChanged += ApplySyncStatus;
            ApplySyncStatus(OfflineOnlineSyncService.CurrentStatus);
        }

        private async Task RunManualSyncAsync()
        {
            var result = await OfflineOnlineSyncService.SyncAsync(SyncDirection.TwoWay);
            SyncStatusText = OfflineOnlineSyncService.CurrentStatus.Message;
            if (!string.IsNullOrWhiteSpace(result.Details))
                CrossSystemStatusText = result.Details;
        }

        private void ApplySyncStatus(SyncStatusSnapshot status)
        {
            void Apply()
            {
                SyncStatusText = status.Message;
                SyncStatusColor = status.Color;
                CrossSystemStatusText = status.CrossSystemMessage;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
                Apply();
            else
                dispatcher.Invoke(Apply);
        }

        private void ChangeProfileImage()
        {
            var path = ProfileImageService.PickAndSaveCurrentProfileImage();
            if (!string.IsNullOrWhiteSpace(path))
                ProfileImagePath = path;
        }

        // ── Navigation ─────────────────────────────────────────────────
        public void NavigateTo(string page)
        {
            NavigateTo(page, closeSidebar: true);
        }

        public void NavigateFromDashboardShortcut(string page)
        {
            IsSidebarVisible = true;
            NavigateTo(page, closeSidebar: false);
        }

        private void NavigateTo(string page, bool closeSidebar)
        {
            if (page == "Beneficiaries")
                page = "Register Member";

            if (AuthService.Instance.IsBeneficiary && page == "Home")
                page = "Beneficiary Portal";

            if (!PermissionService.CanAccessPage(page))
            {
                IsSidebarVisible = false;
                ShowAccessDenied(page);
                return;
            }

            CurrentPageTitle = page;
            if (closeSidebar)
                CloseSidebarMenus();

            if (page == "Review Queue")
            {
                IsSidebarVisible = false;
                OpenReviewQueue();
                return;
            }

            var view = page switch
            {
                "Home" => (System.Windows.Controls.UserControl)new HomeView(),
                "Dashboard" => IsUserRegistrar
                    ? new UserDashboardView()
                    : new DashboardView(),
                "My Profile" => new MyProfileView(),
                "My Policies" => new MyPoliciesView(),
                "My Claims" => new MyClaimsView(),
                "My Premiums" => new MyPremiumsView(),
                "My Benefits" => new MyBenefitsView(),
                "Register Member" => new BeneficiariesView(),
                "Manage Members" => new ManageMembersView(),
                "Employees" => new EmployeesView(),
                "Policies" => new PoliciesView(),
                "Claims" => new ClaimsView(ClaimsListMode.All),
                "All Claims" => new ClaimsView(ClaimsListMode.All),
                "Pending Claims" => new ClaimsView(ClaimsListMode.PendingOnly),
                "Premiums" => new PremiumsView(),
                "Benefits" => new BenefitsView(),
                "Documents" => new DocumentsView(),
                "Transactions" => new TransactionsView(),
                "Source of Funds" => new SourceFundsView(),
                "Barangay Funds" => new SourceFundsView(SourceFundsLandingMode.BarangayFunds),
                "Group Funds" => new SourceFundsView(SourceFundsLandingMode.GroupFunds),
                "Allocated Funds" => new SourceFundsView(SourceFundsLandingMode.AllocatedFunds),
                "Cedulas" => new CedulaManagementView(),
                "Reports" => new ReportsView(),
                "Company Profile" => new CompanyProfileView(),
                "Settings" => new SettingsView(),
                "Beneficiary Portal" => new BeneficiaryPortalView(),
                "Manage Accounts" => new ManageAccountsView(),
                "Announcements" => new AnnouncementsView(),
                _ => new HomeView()
            };

            NavigationService.Instance.NavigateTo(view);
            if (closeSidebar)
                IsSidebarVisible = false;
        }

        private void NavigateFromSidebar(string page)
        {
            var closeSidebar = page == "Home";
            if (closeSidebar)
                CloseSidebarMenus();

            NavigateTo(page, closeSidebar);
        }

        private void CloseSidebarMenus()
        {
            IsMemberManagementPopupOpen = false;
            IsClaimsMenuPopupOpen = false;
            IsBudgetFundsPopupOpen = false;
            IsAdminToolsPopupOpen = false;
        }

        private static void OpenReviewQueue()
        {
            var dialog = new Views.Admin.Dialogs.ClaimQueueDialog(openSelectionInReviewPage: true);
            if (App.ActiveShell is not null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        private static void OpenRestrictedDialog(string page, Action openAction)
        {
            if (!PermissionService.CanAccessPage(page))
            {
                ShowAccessDenied(page);
                return;
            }

            openAction();
        }

        private static void OpenBeneficiaryQueue()
        {
            var queueVm = new BeneficiaryQueueViewModel();
            var dialog = new Views.Admin.Dialogs.BeneficiaryQueueDialog(queueVm);
            if (App.ActiveShell is not null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;

            BeneficiaryQueueItem? selectedItem = null;
            queueVm.OnItemSelected = item => selectedItem = item;

            dialog.ShowDialog();

            if (selectedItem != null)
            {
                var viewVm = new BeneficiaryStagingViewModel();
                if (selectedItem.OriginalSource is BeneficiaryStaging staging)
                {
                    viewVm.SelectedRecord = staging;
                }
                else if (selectedItem.OriginalSource is Beneficiary beneficiary)
                {
                    viewVm.SelectedSystemBeneficiary = beneficiary;
                }
                NavigationService.Instance.NavigateTo(new BeneficiariesView(viewVm, openInitialSearch: false));
            }
        }

        private static void OpenBeneficiaryLookup()
        {
            var dialog = new Views.Admin.Dialogs.BeneficiaryStagingDialog();
            if (App.ActiveShell is not null && App.ActiveShell != dialog)
                dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
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
    }
}
