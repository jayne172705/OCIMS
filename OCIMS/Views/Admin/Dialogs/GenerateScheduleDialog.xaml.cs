using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class GenerateScheduleDialog : Window
    {
        private readonly GenerateScheduleViewModel _vm;

        public GenerateScheduleDialog()
        {
            InitializeComponent();
            _vm = new GenerateScheduleViewModel();
            _vm.CloseAction = () => Close();
            DataContext = _vm;
        }

        public void SetGeneratedCallback(System.Action onGenerated)
            => _vm.OnGenerated = onGenerated;
    }
}
