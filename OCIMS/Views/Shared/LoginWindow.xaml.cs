using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using eSureHi.ViewModels.Shared;

namespace eSureHi.Views.Shared
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _vm;

        public LoginWindow()
        {
            InitializeComponent();
            _vm = new LoginViewModel();

            _vm.GetPassword = () => PasswordBox.Password;

            _vm.OnLoginSuccess = () =>
            {
                Window shell;
                if (eSureHi.Services.AuthService.Instance.IsEmployee)
                {
                    shell = new eSureHi.Views.Admin.EmployeeShell();
                }
                else
                {
                    shell = new eSureHi.Views.Admin.AdminShell();
                }

                shell.Show();
                shell.Dispatcher.BeginInvoke(
                    new Action(() => 
                    {
                        if (shell is eSureHi.Views.Admin.EmployeeShell empShell)
                            empShell.ShowDashboardNotifications();
                        else if (shell is eSureHi.Views.Admin.AdminShell adminShell)
                            adminShell.ShowDashboardNotifications();
                    }),
                    DispatcherPriority.ApplicationIdle);

                Close();
            };

            DataContext = _vm;
            UsernameBox.Focus();
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && _vm.LoginCommand.CanExecute(null))
                _vm.LoginCommand.Execute(null);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_vm.BootstrapCommand.CanExecute(null))
                _vm.BootstrapCommand.Execute(null);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Application.Current.Shutdown();
    }
}
