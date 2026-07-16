using System.Windows;

namespace OCIMS
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                DatabaseHelper.EnsureSchema();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    "Could not connect to the OCIMS database. The app will still open, " +
                    "but saving and loading data will not work until MySQL is running.\n\n" +
                    "Details: " + ex.Message,
                    "OCIMS — Database Unavailable",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
