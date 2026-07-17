using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using eSureHi.ViewModels.Admin;
using eSureHi.Views.Admin.UserControls;
using eSureHi.Services;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class BeneficiarySearchDialog : Window
    {
        public BeneficiarySearchDialog(BeneficiaryStagingViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
            => SelectAndClose();

        private void ResultList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => SelectAndClose();

        private void ResultList_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not null)
                SelectAndClose();
        }

        private void ShowAllButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
            NavigationService.Instance.NavigateTo(new BeneficiariesView(DataContext as BeneficiaryStagingViewModel, openInitialSearch: false));
        }

        private void SelectAndClose()
        {
            if (DataContext is BeneficiaryStagingViewModel { HasSelection: true })
            {
                DialogResult = true;
                Close();
            }
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current is not null)
            {
                if (current is T match)
                    return match;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
