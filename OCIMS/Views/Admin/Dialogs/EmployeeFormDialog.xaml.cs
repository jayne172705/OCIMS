using System.Threading.Tasks;
using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class EmployeeFormDialog : Window
    {
        private readonly EmployeeFormViewModel _vm;

        // ── New Employee ───────────────────────────────────────────────
        public EmployeeFormDialog()
        {
            InitializeComponent();
            _vm = new EmployeeFormViewModel();
            _vm.CloseAction = () => Close();
            DataContext = _vm;
        }

        // ── Edit Employee ──────────────────────────────────────────────
        public EmployeeFormDialog(int empId) : this()
        {
            _ = _vm.InitEditAsync(empId);
        }

        public async Task InitAsync()
        {
            await _vm.InitNewAsync();
        }

        public void SetSaveCallback(System.Action onSave)
        {
            _vm.OnSaveSuccess = onSave;
        }
    }
}
