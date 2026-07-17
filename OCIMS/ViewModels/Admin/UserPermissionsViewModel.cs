using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;

namespace eSureHi.ViewModels.Admin
{
    public class UserPermissionsViewModel : ObservableObject
    {
        private readonly int _userId;

        public string UserHeader { get; }
        public string Role { get; }

        private string _errorMessage = string.Empty;
        private bool _isBusy;

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
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

        private bool _canAccessDashboard = true;
        private bool _canAccessEmployees = true;
        private bool _canAccessBeneficiaries = true;
        private bool _canAccessPolicies = true;
        private bool _canAccessClaims = true;
        private bool _canAccessPremiums = true;
        private bool _canAccessBenefits = true;
        private bool _canAccessDocuments = true;
        private bool _canAccessTransactions = true;
        private bool _canAccessCedulas = true;
        private bool _canAccessReports = true;
        private bool _canAccessCompanyProfile = true;
        private bool _canAccessSettings = true;

        public bool CanAccessDashboard { get => _canAccessDashboard; set => SetProperty(ref _canAccessDashboard, value); }
        public bool CanAccessEmployees { get => _canAccessEmployees; set => SetProperty(ref _canAccessEmployees, value); }
        public bool CanAccessBeneficiaries { get => _canAccessBeneficiaries; set => SetProperty(ref _canAccessBeneficiaries, value); }
        public bool CanAccessPolicies { get => _canAccessPolicies; set => SetProperty(ref _canAccessPolicies, value); }
        public bool CanAccessClaims { get => _canAccessClaims; set => SetProperty(ref _canAccessClaims, value); }
        public bool CanAccessPremiums { get => _canAccessPremiums; set => SetProperty(ref _canAccessPremiums, value); }
        public bool CanAccessBenefits { get => _canAccessBenefits; set => SetProperty(ref _canAccessBenefits, value); }
        public bool CanAccessDocuments { get => _canAccessDocuments; set => SetProperty(ref _canAccessDocuments, value); }
        public bool CanAccessTransactions { get => _canAccessTransactions; set => SetProperty(ref _canAccessTransactions, value); }
        public bool CanAccessCedulas { get => _canAccessCedulas; set => SetProperty(ref _canAccessCedulas, value); }
        public bool CanAccessReports { get => _canAccessReports; set => SetProperty(ref _canAccessReports, value); }
        public bool CanAccessCompanyProfile { get => _canAccessCompanyProfile; set => SetProperty(ref _canAccessCompanyProfile, value); }
        public bool CanAccessSettings { get => _canAccessSettings; set => SetProperty(ref _canAccessSettings, value); }

        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public Action? CloseAction { get; set; }
        public Action? OnSaveSuccess { get; set; }

        public UserPermissionsViewModel(SystemUser user)
        {
            _userId = user.UserId;
            Role = user.Role;
            UserHeader = $"{user.Username} - {user.Role}";

            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsBusy);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());

            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var permission = await db.UserPermissions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == _userId);

                if (permission is null)
                    return;

                CanAccessDashboard = permission.CanAccessDashboard;
                CanAccessEmployees = permission.CanAccessEmployees;
                CanAccessBeneficiaries = permission.CanAccessBeneficiaries;
                CanAccessPolicies = permission.CanAccessPolicies;
                CanAccessClaims = permission.CanAccessClaims;
                CanAccessPremiums = permission.CanAccessPremiums;
                CanAccessBenefits = permission.CanAccessBenefits;
                CanAccessDocuments = permission.CanAccessDocuments;
                CanAccessTransactions = permission.CanAccessTransactions;
                CanAccessCedulas = permission.CanAccessCedulas;
                CanAccessReports = permission.CanAccessReports;
                CanAccessCompanyProfile = permission.CanAccessCompanyProfile;
                CanAccessSettings = permission.CanAccessSettings;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var permission = await db.UserPermissions
                    .FirstOrDefaultAsync(p => p.UserId == _userId);

                if (permission is null)
                {
                    permission = new UserPermission { UserId = _userId };
                    db.UserPermissions.Add(permission);
                }

                permission.CanAccessDashboard = CanAccessDashboard;
                permission.CanAccessEmployees = CanAccessEmployees;
                permission.CanAccessBeneficiaries = CanAccessBeneficiaries;
                permission.CanAccessPolicies = CanAccessPolicies;
                permission.CanAccessClaims = CanAccessClaims;
                permission.CanAccessPremiums = CanAccessPremiums;
                permission.CanAccessBenefits = CanAccessBenefits;
                permission.CanAccessDocuments = CanAccessDocuments;
                permission.CanAccessTransactions = CanAccessTransactions;
                permission.CanAccessCedulas = CanAccessCedulas;
                permission.CanAccessReports = CanAccessReports;
                permission.CanAccessCompanyProfile = CanAccessCompanyProfile;
                permission.CanAccessSettings = CanAccessSettings;

                await db.SaveChangesAsync();
                OnSaveSuccess?.Invoke();
                MessageBox.Show(
                    "Access settings saved. Changes take effect on the user's next login.",
                    "Permissions Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Save failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
