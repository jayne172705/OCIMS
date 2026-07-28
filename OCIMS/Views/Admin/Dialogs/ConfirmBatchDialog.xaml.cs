using System.Windows.Controls;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class ConfirmBatchDialog : UserControl
    {
        public ConfirmBatchDialog(object viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
