using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class EmployeeSummaryDialog : Window
    {
        private readonly EmployeeSummaryViewModel _vm;

        public EmployeeSummaryDialog(int empId)
        {
            InitializeComponent();
            _vm = new EmployeeSummaryViewModel(empId);
            _vm.CloseAction = () => Close();
            DataContext = _vm;
            Loaded += async (s, e) => await _vm.LoadAsync();
        }
    }
}
