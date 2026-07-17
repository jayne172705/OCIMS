using System.Windows;
using eSureHi.Helpers;
using eSureHi.Views.Shared; // Added this

namespace eSureHi.ViewModels.Shared
{
    public class GettingStartedViewModel : ObservableObject
    {
        public RelayCommand ContinueCommand { get; }

        public GettingStartedViewModel()
        {
            ContinueCommand = new RelayCommand(ContinueToLogin);
        }

        private void ContinueToLogin()
        {
            // Open the Login Window
            var loginWindow = new LoginWindow(); // Corrected this
            loginWindow.Show();

            // Find the current window and close it
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.Close();
                    break;
                }
            }
        }
    }
}
