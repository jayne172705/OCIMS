using System.Windows.Controls;
using System.Windows.Input;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class BenefitsView : UserControl
    {
        public BenefitsView() => InitializeComponent();

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is BenefitsViewModel vm && vm.SelectedBenefit is not null)
                vm.EditCommand.Execute(null);
        }
    }
}
