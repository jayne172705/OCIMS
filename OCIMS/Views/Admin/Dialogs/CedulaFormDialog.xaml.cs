using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class CedulaFormDialog : Window
    {
        private readonly CedulaFormViewModel _vm;

        public CedulaFormDialog(int? cedulaId = null)
        {
            InitializeComponent();
            _vm = new CedulaFormViewModel(cedulaId);
            _vm.CloseAction = () => { DialogResult = false; Close(); };
            _vm.OnSaveSuccess = () => { DialogResult = true; Close(); };
            DataContext = _vm;
        }
    }
}
