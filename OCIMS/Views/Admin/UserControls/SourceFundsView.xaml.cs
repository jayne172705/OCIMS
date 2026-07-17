using System.Windows.Controls;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class SourceFundsView : UserControl
    {
        public SourceFundsView() : this(SourceFundsLandingMode.Overview)
        {
        }

        public SourceFundsView(SourceFundsLandingMode landingMode)
        {
            InitializeComponent();
            DataContext = new SourceFundsViewModel(landingMode);
        }
    }
}
