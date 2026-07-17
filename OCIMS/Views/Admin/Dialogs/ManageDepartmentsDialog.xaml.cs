using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class ManageDepartmentsDialog : Window
    {
        private readonly DepartmentsViewModel _vm;

        public ManageDepartmentsDialog()
        {
            InitializeComponent();
            _vm = new DepartmentsViewModel();
            _vm.CloseAction = () => Close();
            DataContext = _vm;
        }
    }
}
