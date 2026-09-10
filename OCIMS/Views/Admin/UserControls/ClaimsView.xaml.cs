using System.Windows.Controls;
using System.Windows.Input;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Views.Admin.UserControls
{
    public partial class ClaimsView : UserControl
    {
        public ClaimsView() : this(ClaimsListMode.All)
        {
        }

        public ClaimsView(ClaimsListMode listMode)
        {
            InitializeComponent();
            DataContext = new ClaimsViewModel(listMode);
            Loaded += UserControl_Loaded;
        }

        public ClaimsView(ClaimsListMode listMode, string initialSearch, string initialProgram)
        {
            InitializeComponent();
            DataContext = new ClaimsViewModel(listMode, initialSearch, initialProgram);
            Loaded += UserControl_Loaded;
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ClaimsViewModel vm && vm.IsAllMode)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new System.Action(() =>
                {
                    OpenSearchDialog(vm);
                }));
            }
        }

        private async void OpenSearchDialog(ClaimsViewModel vm)
        {
            var dialog = new Dialogs.ClaimSearchDialog(vm);
            await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "ClaimsDialogHost");
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ClaimsViewModel vm && vm.SelectedClaim is not null)
                vm.ViewCommand.Execute(null);
        }
    }
}
