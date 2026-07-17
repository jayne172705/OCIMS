using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class TrainingFormDialog : Window
    {
        private readonly TrainingFormViewModel _vm;

        public TrainingFormDialog(int empId)
        {
            InitializeComponent();
            _vm = new TrainingFormViewModel(empId);
            _vm.CloseAction = () => { DialogResult = false; Close(); };
            _vm.OnSaveSuccess = () => { DialogResult = true; Close(); };
            DataContext = _vm;
        }
    }
}
