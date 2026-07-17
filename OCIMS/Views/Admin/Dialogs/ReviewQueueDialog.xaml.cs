using System.Windows;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class ReviewQueueDialog : Window
    {
        private readonly ReviewQueueViewModel _vm;

        public ReviewQueueDialog()
        {
            InitializeComponent();
            _vm = new ReviewQueueViewModel();
            _vm.CloseAction = () => Close();
            DataContext = _vm;
        }
    }
}
