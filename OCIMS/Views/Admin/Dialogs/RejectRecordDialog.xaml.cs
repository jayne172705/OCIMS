using System.Windows.Controls;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class RejectRecordDialog : UserControl
    {
        public RejectRecordDialog(object viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
