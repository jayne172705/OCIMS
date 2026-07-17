using System;
using System.Windows;
using System.Windows.Threading;
using eSureHi.Services;
using eSureHi.ViewModels.Admin;
using eSureHi.Views.Shared;
using MaterialDesignThemes.Wpf;

namespace eSureHi.Views.Admin
{
    public partial class AdminShell : Window
    {
        private readonly AdminShellViewModel _vm;
        private readonly DispatcherTimer _clockTimer;

        public AdminShell()
        {
            InitializeComponent();
            SourceInitialized += (_, _) => ConstrainToWorkArea();

            _vm = new AdminShellViewModel();
            _vm.LogoutAction = () =>
            {
                AuthService.Instance.Logout();
                _clockTimer?.Stop();
                new LoginWindow().Show();
                Close();
            };

            DataContext = _vm;
            App.ActiveShell = this;

            // Clock timer - updates title bar every second
            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _clockTimer.Tick += (s, e) =>
            {
                if (CurrentDateTime is not null)
                    CurrentDateTime.Text =
                        DateTime.Now.ToString("ddd, MMM d yyyy  h:mm:ss tt");
            };
            _clockTimer.Start();

            // Update maximize icon on state change
            StateChanged += (s, e) =>
            {
                // ✅ Null-conditional operator prevents CS8602 warning
                if (MaximizeIcon is not null)
                    MaximizeIcon.Kind = WindowState == WindowState.Maximized
                        ? PackIconKind.WindowRestore
                        : PackIconKind.WindowMaximize;

                if (WindowState == WindowState.Maximized)
                    ConstrainToWorkArea();
            };
        }

        private void AdminShell_Loaded(object sender, RoutedEventArgs e)
        {
            if (!AuthService.Instance.IsAdmin && !AuthService.Instance.IsEmployee && !AuthService.Instance.IsBeneficiary)
            {
                _clockTimer.Stop();
                new LoginWindow().Show();
                Close();
                return;
            }

            ConstrainToWorkArea();
            NavigationService.Instance.SetFrame(MainContent);
            _vm.NavigateTo(AuthService.Instance.IsBeneficiary
                ? "Beneficiary Portal"
                : PermissionService.FirstAllowedAdminPage());
        }

        private void ConstrainToWorkArea()
        {
            var workArea = SystemParameters.WorkArea;

            MaxWidth = workArea.Width;
            MaxHeight = workArea.Height;

            if (WindowState == WindowState.Maximized)
            {
                Left = workArea.Left;
                Top = workArea.Top;
                Width = MaxWidth;
                Height = MaxHeight;
            }
        }

        public async void ShowDashboardNotifications()
        {
            try
            {
                await _vm.ShowLoginNotificationAsync();
                Activate();

                var username = AuthService.Instance.CurrentUser?.Username ?? "User";
                var role = AuthService.Instance.CurrentUser?.Role ?? "User";
                var isEmployee = AuthService.Instance.IsEmployee;
                var isBeneficiary = AuthService.Instance.IsBeneficiary;
                var title = isEmployee
                    ? "Employee Notification"
                    : isBeneficiary
                        ? "Beneficiary Notification"
                    : "System Notification";
                var message = isEmployee
                    ? $"Welcome to eSureHi+, {username}!\n\nYou are signed in as Employee.\nYou can access your own profile, policies, claims, premiums, benefits, and employee notifications only."
                    : isBeneficiary
                        ? $"Welcome to eSureHi+, {username}!\n\nYou are signed in as Beneficiary.\nYou can submit insurance claims, upload requirements, and update your email or password."
                    : $"Welcome to eSureHi+, {username}!\n\nYou are signed in as {role}.\nPlease review your dashboard updates and pending insurance records.";
                var dialog = new SystemNotificationDialog(
                    title,
                    message);

                dialog.Owner = this;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                App.ReportError("Admin Notification Failed", ex);
            }
        }

        // ── Window Controls ────────────────────────────────────────────
        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to exit eSureHi?",
                "Confirm Exit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
                Application.Current.Shutdown();
        }
    }
}
