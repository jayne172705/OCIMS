using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class ClaimQueueDialog : Window
    {
        private readonly ClaimQueueViewModel _vm;

        public ClaimQueueDialog(bool openSelectionInReviewPage = false)
        {
            InitializeComponent();
            _vm = new ClaimQueueViewModel(openSelectionInReviewPage);
            _vm.CloseAction = () => Close();
            DataContext = _vm;
        }
    }
}
