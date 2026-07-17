using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;

namespace eSureHi.ViewModels.Admin
{
    public class CompanyProfileViewModel : ObservableObject
    {
        private CompanyProfile _profile = new();
        private bool _isBusy;

        public CompanyProfile Profile
        {
            get => _profile;
            set => SetProperty(ref _profile, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                SetProperty(ref _isBusy, value);
                SaveCommand.RaiseCanExecuteChanged();
            }
        }

        public RelayCommand SaveCommand { get; }
        public RelayCommand LoadCommand { get; }
        public RelayCommand BrowseLogoCommand { get; }
        public RelayCommand ClearLogoCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        public CompanyProfileViewModel()
        {
            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsBusy);
            LoadCommand = new RelayCommand(async () => await LoadAsync());
            BrowseLogoCommand = new RelayCommand(BrowseLogo);
            ClearLogoCommand = new RelayCommand(ClearLogo);
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);
            
            // Load initially
            _ = LoadAsync();
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }

        private void BrowseLogo()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Company Logo",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                Profile.LogoPath = dialog.FileName;
                OnPropertyChanged(nameof(Profile));
            }
        }

        private void ClearLogo()
        {
            Profile.LogoPath = string.Empty;
            OnPropertyChanged(nameof(Profile));
        }

        public async Task LoadAsync()
        {
            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var existing = await db.CompanyProfiles.FirstOrDefaultAsync();
                if (existing != null)
                {
                    Profile = existing;
                }
                else
                {
                    Profile = new CompanyProfile { Name = "Municipality of Sulop" };
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load company profile: {ex.Message}", "Error");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task SaveAsync()
        {
            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var existing = await db.CompanyProfiles.FirstOrDefaultAsync(c => c.Id == Profile.Id);
                
                if (existing != null)
                {
                    existing.Name = Profile.Name;
                    existing.Address = Profile.Address;
                    existing.ContactNumber = Profile.ContactNumber;
                    existing.Email = Profile.Email;
                    existing.LogoPath = Profile.LogoPath;
                    db.CompanyProfiles.Update(existing);
                }
                else
                {
                    db.CompanyProfiles.Add(Profile);
                }
                
                await db.SaveChangesAsync();
                MessageBox.Show("Company profile saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save company profile: {ex.Message}", "Error");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
