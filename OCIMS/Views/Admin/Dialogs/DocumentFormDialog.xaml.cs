using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class DocumentFormDialog : Window
    {
        private readonly DocumentFormViewModel _vm;

        public DocumentFormDialog()
        {
            InitializeComponent();
            _vm = new DocumentFormViewModel();
            _vm.CloseAction = () => Close();
            DataContext = _vm;
        }

        public DocumentFormDialog(int docId) : this()
        {
            _ = _vm.InitEditAsync(docId);
        }

        public void SetSaveCallback(System.Action onSave)
            => _vm.OnSaveSuccess = onSave;
    }
}
