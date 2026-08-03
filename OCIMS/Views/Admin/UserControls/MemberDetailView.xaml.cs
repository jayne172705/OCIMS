using System.Windows.Controls;
using eSureHi.Models;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class MemberDetailView : UserControl
    {
        public MemberDetailView(Beneficiary member)
        {
            InitializeComponent();
            DataContext = new MemberDetailViewModel(member);
        }
    }
}
