using System;
using System.Windows;
using System.Windows.Threading;
using eSureHi.Services;
using eSureHi.ViewModels.Admin;
using eSureHi.Views.Shared;
using MaterialDesignThemes.Wpf;

namespace eSureHi.Views.Admin
{
    public partial class EmployeeShell : Window
    {
        private readonly EmployeeShellViewModel _vm;
        private readonly DispatcherTimer _clockTimer;

        public EmployeeShell()
        {
            InitializeComponent();

            _vm = new EmployeeShellViewModel();
            _vm.LogoutAction = () =>
            {
                AuthService.Instance.Logout();
                _clockTimer?.Stop();
                new LoginWindow().Show();
                Close();
            };

            DataContext = _vm;
            App.ActiveShell = this;

            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            _clockTimer.Tick += (s, e) =>
            {
                // Null check prevents CS8602 warning
                if (CurrentDateTime is not null)
                    CurrentDateTime.Text =
                        DateTime.Now.ToString("dddd, MMMM d, yyyy   hh:mm:ss tt");
            };

            StateChanged += (s, e) =>
            {
                // Null check prevents CS8602 warning
                if (MaximizeIcon is not null)
                    MaximizeIcon.Kind = WindowState == WindowState.Maximized
                        ? PackIconKind.WindowRestore
                        : PackIconKind.WindowMaximize;
            };
            _clockTimer.Start();
        }

        private void EmployeeShell_Loaded(object sender, RoutedEventArgs e)
        {
            NavigationService.Instance.SetFrame(MainContent);
            _vm.NavigateTo("Dashboard");
        }

        public async void ShowDashboardNotifications()
        {
            try
            {
                await _vm.ShowLoginNotificationAsync();
                Activate();

                var username = AuthService.Instance.CurrentUser?.Username ?? "User";
                var dialog = new SystemNotificationDialog(
                    "Employee Notification",
                    $"Welcome to eSureHi+, {username}!\n\nYou are signed in as Employee.\nYou can access your own profile, policies, claims, premiums, benefits, and employee notifications only.");

                dialog.Owner = this;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                App.ReportError("Employee Notification Failed", ex);
            }
        }

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
