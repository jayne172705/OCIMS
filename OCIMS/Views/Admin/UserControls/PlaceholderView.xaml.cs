using System.Windows.Controls;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class PlaceholderView : UserControl
    {
        public PlaceholderView(string? title = null)
        {
            InitializeComponent();
            if (!string.IsNullOrWhiteSpace(title))
                TitleText.Text = title;
        }
    }
}
