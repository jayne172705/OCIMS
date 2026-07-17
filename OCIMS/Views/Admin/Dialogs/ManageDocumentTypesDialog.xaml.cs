using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class ManageDocumentTypesDialog : Window
    {
        private readonly DocumentTypeViewModel _vm;

        public ManageDocumentTypesDialog()
        {
            InitializeComponent();
            _vm = new DocumentTypeViewModel();
            _vm.CloseAction = () => Close();
            DataContext = _vm;
        }
    }
}
