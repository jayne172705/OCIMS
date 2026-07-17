using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;

namespace eSureHi.ViewModels.Admin
{
    public class MyClaimsViewModel : ObservableObject
    {
        public ObservableCollection<Claim> Claims { get; } = new();

        private Claim? _selectedClaim;
        public Claim? SelectedClaim
        {
            get => _selectedClaim;
            set
            {
                SetProperty(ref _selectedClaim, value);
                OnPropertyChanged(nameof(HasSelection));
                ViewCommand.RaiseCanExecuteChanged();
            }
        }
        public bool HasSelection => SelectedClaim is not null;

        private bool _isLoading;
        private bool _hasData;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public bool HasData { get => _hasData; set => SetProperty(ref _hasData, value); }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand ViewCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        public MyClaimsViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            ViewCommand = new RelayCommand(OpenDetailDialog,
                                () => SelectedClaim is not null);
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);
            _ = LoadAsync();
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(
                AuthService.Instance.IsEmployee
                    ? new Views.Admin.UserControls.EmployeeDashboardView()
                    : new Views.Admin.UserControls.HomeView());
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var empId = AuthService.Instance.CurrentEmployee?.EmpId;
                if (empId is null) return;

                using var db = eSureHiDbContextFactory.Create();
                var list = await db.Claims
                    .Include(c => c.Policy)
                    .Where(c => c.EmpId == empId)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                Claims.Clear();
                foreach (var c in list) Claims.Add(c);
                HasData = Claims.Count > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load claims failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsLoading = false; }
        }

        private void OpenDetailDialog()
        {
            if (SelectedClaim is null) return;
            var dialog = new Views.Admin.Dialogs.ClaimDetailDialog(
                SelectedClaim.ClaimId,
                isReadOnly: true);
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }
    }
}
