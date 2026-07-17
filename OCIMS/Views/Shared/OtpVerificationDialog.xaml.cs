using System.Windows;
using System.Windows.Input;

namespace eSureHi.Views.Shared
{
    public partial class OtpVerificationDialog : Window
    {
        public OtpVerificationDialog()
        {
            InitializeComponent();
            PinBox.Focus();
        }

        private void VerifyButton_Click(object sender, RoutedEventArgs e)
        {
            if (PinBox.Password == "123456")
            {
                DialogResult = true;
                Close();
            }
            else
            {
                ErrorText.Visibility = Visibility.Visible;
                PinBox.Clear();
                PinBox.Focus();
            }
        }
    }
}
