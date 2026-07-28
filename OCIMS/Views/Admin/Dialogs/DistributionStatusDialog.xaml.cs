using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class DistributionStatusDialog : Window
    {
        public DistributionStatusDialog(int beneficiaryId)
        {
            InitializeComponent();
            var vm = new DistributionStatusViewModel(beneficiaryId);
            vm.CloseAction = () => Close();
            DataContext = vm;
        }
    }
}
