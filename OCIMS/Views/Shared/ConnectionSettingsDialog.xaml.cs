using System.Windows;
using eSureHi.ViewModels.Shared;

namespace eSureHi.Views.Shared
{
    public partial class ConnectionSettingsDialog : Window
    {
        public ConnectionSettingsDialog()
        {
            InitializeComponent();

            var vm = new ConnectionSettingsViewModel
            {
                CloseAction = () => Close()
            };

            DataContext = vm;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }
    }
}
