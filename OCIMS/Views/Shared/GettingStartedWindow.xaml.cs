using System.Windows;
using eSureHi.ViewModels.Shared;

namespace eSureHi.Views.Shared
{
    public partial class GettingStartedWindow : Window
    {
        public GettingStartedWindow()
        {
            InitializeComponent();
            DataContext = new GettingStartedViewModel();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Application.Current.Shutdown();
    }
}
