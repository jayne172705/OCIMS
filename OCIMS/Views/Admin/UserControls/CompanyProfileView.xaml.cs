using System.Windows.Controls;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class CompanyProfileView : UserControl
    {
        public CompanyProfileView()
        {
            InitializeComponent();
            DataContext = new CompanyProfileViewModel();
        }
    }
}