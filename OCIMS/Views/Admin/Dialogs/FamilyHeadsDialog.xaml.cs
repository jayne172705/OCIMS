using System.Windows;
using Microsoft.EntityFrameworkCore;
using eSureHi.Models;
using eSureHi.ViewModels.Admin.Dialogs;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class FamilyHeadsDialog : Window
    {
        private readonly FamilyHeadsViewModel _viewModel;

        public FamilyHeadsDialog(string barangay)
        {
            InitializeComponent();
            _viewModel = new FamilyHeadsViewModel(barangay)
            {
                OpenProfileRequested = OpenProfile,
                AddBeneficiaryRequested = AddBeneficiary,
                RegisterResidentRequested = async display => await RegisterResidentAsync(display)
            };
            DataContext = _viewModel;

            Loaded += async (s, e) => await _viewModel.LoadAsync();
        }

        private void OpenProfile(Employee selectedHead)
        {
            var profileDialog = new FamilyHeadProfileDialog(selectedHead);
            profileDialog.Owner = this;
            profileDialog.ShowDialog();
        }

        private async void AddBeneficiary()
        {
            var dialog = new EmployeeFormDialog(_viewModel.Barangay);
            await dialog.InitAsync();
            dialog.Owner = this;
            dialog.SetSaveCallback(async () => await _viewModel.LoadAsync());
            dialog.ShowDialog();
        }

        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && _viewModel != null)
            {
                _viewModel.SearchQuery = textBox.Text;
            }
        }

        private async System.Threading.Tasks.Task RegisterResidentAsync(BarangayBeneficiaryDisplay display)
        {
            if (display.CrsRecord == null) return;

            eSureHi.Models.BeneficiaryStaging? staging;

            using (var db = eSureHi.Data.eSureHiDbContextFactory.Create())
            {
                var crs = display.CrsRecord;
                staging = await db.BeneficiaryStaging
                    .FirstOrDefaultAsync(s => 
                        s.BeneficiaryId == crs.BeneficiaryId || 
                        (crs.CivilRegistryId != null && s.CivilRegistryId == crs.CivilRegistryId));

                if (staging == null)
                {
                    staging = new eSureHi.Models.BeneficiaryStaging
                    {
                        BeneficiaryId = crs.BeneficiaryId,
                        ResidentsId = crs.ResidentsId,
                        CivilRegistryId = crs.CivilRegistryId,
                        LastName = crs.LastName,
                        FirstName = crs.FirstName,
                        MiddleName = crs.MiddleName,
                        FullName = crs.FullName,
                        Sex = crs.Sex,
                        DateOfBirth = crs.DateOfBirth,
                        Age = crs.Age,
                        MaritalStatus = crs.MaritalStatus,
                        Address = crs.Address,
                        IsPwd = crs.IsPwd,
                        IsSenior = crs.IsSenior,
                        CedulaNo = crs.CedulaNo,
                        LinkStatus = "Unlinked"
                    };
                    db.BeneficiaryStaging.Add(staging);
                    await db.SaveChangesAsync();
                }
            }

            var dialog = new RegisterPrimaryDialog(staging!);
            dialog.Owner = this;
            dialog.ShowDialog();
            await _viewModel.LoadAsync();
        }
    }
}
