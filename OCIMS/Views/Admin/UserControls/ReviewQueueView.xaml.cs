using System.Windows.Controls;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class ReviewQueueView : UserControl
    {
        public ReviewQueueView()
            : this(null, false)
        {
        }

        public ReviewQueueView(int claimId)
            : this(claimId, true)
        {
        }

        private ReviewQueueView(int? claimId, bool detailOnly)
        {
            InitializeComponent();
            DataContext = new ReviewQueueClaimsViewModel(claimId, detailOnly);
        }
    }
}
