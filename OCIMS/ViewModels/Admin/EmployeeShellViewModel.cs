using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Services;
using eSureHi.Views.Admin.UserControls;

namespace eSureHi.ViewModels.Admin
{
    public class EmployeeShellViewModel : ObservableObject
    {
        // ── Page Title ─────────────────────────────────────────────────
        private string _currentPageTitle = "Dashboard";
        public string CurrentPageTitle
        {
            get => _currentPageTitle;
            set => SetProperty(ref _currentPageTitle, value);
        }

        // ── User Info ──────────────────────────────────────────────────
        public string CurrentUsername =>
            AuthService.Instance.CurrentUser?.Username ?? "Employee";
        public string CurrentRole =>
            AuthService.Instance.CurrentUser?.Role ?? "Employee";
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
        public string EmployeeFullName =>
            AuthService.Instance.CurrentEmployee?.FullName ?? CurrentUsername;

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

        // ── Notifications Panel ────────────────────────────────────────
        private NotificationsViewModel? _notificationsVm;
        public NotificationsViewModel NotificationsVm
        {
            get
            {
                if (_notificationsVm is null)
                {
                    _notificationsVm = new NotificationsViewModel();
                    _notificationsVm.OnUnreadCountChanged =
                        count => UnreadNotifications = count;
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

        public RelayCommand ToggleNotificationsCommand { get; }
        public RelayCommand ChangeProfileImageCommand { get; }

        // ── Logout ─────────────────────────────────────────────────────
        public Action? LogoutAction { get; set; }
        public RelayCommand LogoutCommand { get; }

        // ── Nav Commands ───────────────────────────────────────────────
        public RelayCommand NavDashboardCommand { get; }
        public RelayCommand NavMyProfileCommand { get; }
        public RelayCommand NavMyPoliciesCommand { get; }
        public RelayCommand NavMyClaimsCommand { get; }
        public RelayCommand NavMyPremiumsCommand { get; }
        public RelayCommand NavMyBenefitsCommand { get; }
        public RelayCommand NavBeneficiariesCommand { get; }
        public RelayCommand NavTransactionsCommand { get; }

        // ── Constructor ────────────────────────────────────────────────
        public EmployeeShellViewModel()
        {
            LogoutCommand = new RelayCommand(() => LogoutAction?.Invoke());
            ChangeProfileImageCommand = new RelayCommand(ChangeProfileImage);

            ToggleNotificationsCommand = new RelayCommand(() =>
            {
                NavigateTo("Dashboard");
                IsNotificationsPanelOpen = true;
                _ = NotificationsVm.LoadAsync();
            });

            NavDashboardCommand = new RelayCommand(() => NavigateTo("Dashboard"));
            NavMyProfileCommand = new RelayCommand(() => NavigateTo("My Profile"));
            NavMyPoliciesCommand = new RelayCommand(() => NavigateTo("My Policies"));
            NavMyClaimsCommand = new RelayCommand(() => NavigateTo("My Claims"));
            NavMyPremiumsCommand = new RelayCommand(() => NavigateTo("My Premiums"));
            NavMyBenefitsCommand = new RelayCommand(() => NavigateTo("My Benefits"));
            NavBeneficiariesCommand = new RelayCommand(() => NavigateTo("Beneficiaries"));
            NavTransactionsCommand = new RelayCommand(() => NavigateTo("Transactions"));

            _ = LoadNotificationCountAsync();
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
            CurrentPageTitle = page;
            var view = page switch
            {
                "Dashboard" => (System.Windows.Controls.UserControl)new EmployeeDashboardView(),
                "My Profile" => (System.Windows.Controls.UserControl)new MyProfileView(),
                "My Policies" => new MyPoliciesView(),
                "My Claims" => new MyClaimsView(),
                "My Premiums" => new MyPremiumsView(),
                "My Benefits" => new MyBenefitsView(),
                "Beneficiaries" => new BeneficiariesView(),
                "Transactions" => new TransactionsView(),
                _ => new EmployeeDashboardView()
            };
            NavigationService.Instance.NavigateTo(view);
        }

        // ── Notification Count ─────────────────────────────────────────
        private async Task LoadNotificationCountAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var userId = AuthService.Instance.CurrentUser?.UserId ?? 0;
                UnreadNotifications = await db.Notifications
                    .CountAsync(n => n.RecipientId == userId && !n.IsRead);
            }
            catch { UnreadNotifications = 0; }
        }

        public async Task ShowLoginNotificationAsync()
        {
            NavigateTo("Dashboard");
            await NotificationsVm.LoadAsync();
            IsNotificationsPanelOpen = false;
        }
    }
}
