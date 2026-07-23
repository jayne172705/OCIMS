using System.Windows.Controls;
using eSureHi.Models;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class AdvancePaymentView : UserControl
    {
        public AdvancePaymentView()
        {
            InitializeComponent();
            DataContext = new AdvancePaymentViewModel();
        }

        private void SearchResult_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is Beneficiary beneficiary &&
                DataContext is AdvancePaymentViewModel vm)
            {
                vm.SelectBeneficiaryCommand.Execute(beneficiary);
            }
        }
    }
}
