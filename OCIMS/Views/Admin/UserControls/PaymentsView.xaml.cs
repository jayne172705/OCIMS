using System.Windows.Controls;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class PaymentsView : UserControl
    {
        public PaymentsView() : this(PaymentsListMode.All)
        {
        }

        public PaymentsView(PaymentsListMode listMode)
        {
            InitializeComponent();
            DataContext = new PaymentsViewModel(listMode);
        }
    }
}
