using System.Windows;
using System.Windows.Controls;
using eSureHi.Models;
using MaterialDesignThemes.Wpf;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class PrintIdCardDialog : UserControl
    {
        public PrintIdCardDialog(BeneficiaryStaging beneficiary)
        {
            InitializeComponent();
            DataContext = beneficiary;
        }

        private void PrintBtn_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                printDialog.PrintVisual(IdCardVisual, "Beneficiary ID Card");
                DialogHost.CloseDialogCommand.Execute(null, this);
            }
        }
    }
}
