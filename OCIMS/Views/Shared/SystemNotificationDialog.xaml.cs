using System.Windows;

namespace eSureHi.Views.Shared
{
    public partial class SystemNotificationDialog : Window
    {
        public string NotificationTitle { get; }
        public string NotificationMessage { get; }

        public SystemNotificationDialog(string title, string message)
        {
            NotificationTitle = title;
            NotificationMessage = message;
            DataContext = this;

            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
