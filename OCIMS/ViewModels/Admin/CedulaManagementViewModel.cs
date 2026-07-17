using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;

namespace eSureHi.ViewModels.Admin
{
    public class CedulaManagementViewModel : ObservableObject
    {
        public ObservableCollection<Cedula> Cedulas { get; } = new();

        private Cedula? _selectedCedula;
        public Cedula? SelectedCedula
        {
            get => _selectedCedula;
            set
            {
                SetProperty(ref _selectedCedula, value);
                EditCommand.RaiseCanExecuteChanged();
                DeleteCommand.RaiseCanExecuteChanged();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { SetProperty(ref _searchText, value); _ = LoadAsync(); }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        public CedulaManagementViewModel()
        {
            AddCommand = new RelayCommand(OpenAddDialog);
            EditCommand = new RelayCommand(OpenEditDialog, () => SelectedCedula is not null);
            DeleteCommand = new RelayCommand(async () => await DeleteAsync(), () => SelectedCedula is not null);
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);

            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var query = db.Cedulas
                    .Include(c => c.Employee)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    var s = SearchText.Trim().ToLower();
                    query = query.Where(c =>
                        c.CedulaNo.ToLower().Contains(s) ||
                        c.PlaceIssued.ToLower().Contains(s) ||
                        (c.Employee != null && c.Employee.FullName.ToLower().Contains(s)) ||
                        (c.Employee != null && c.Employee.EmployeeNo.ToLower().Contains(s)));
                }

                var list = await query
                    .OrderByDescending(c => c.IssueDate)
                    .ToListAsync();
                Cedulas.Clear();
                foreach (var c in list) Cedulas.Add(c);
                StatusMessage = $"{Cedulas.Count} cedula record(s) shown.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OpenAddDialog()
        {
            var dialog = new Views.Admin.Dialogs.CedulaFormDialog();
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            if (dialog.ShowDialog() == true)
            {
                _ = LoadAsync();
            }
        }

        private void OpenEditDialog()
        {
            if (SelectedCedula == null) return;
            var dialog = new Views.Admin.Dialogs.CedulaFormDialog(SelectedCedula.Id);
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            if (dialog.ShowDialog() == true)
            {
                _ = LoadAsync();
            }
        }

        private async Task DeleteAsync()
        {
            if (SelectedCedula == null) return;
            var res = MessageBox.Show($"Are you sure you want to delete Cedula {SelectedCedula.CedulaNo}?", "Confirm", MessageBoxButton.YesNo);
            if (res == MessageBoxResult.Yes)
            {
                using var db = eSureHiDbContextFactory.Create();
                var toDelete = await db.Cedulas.FindAsync(SelectedCedula.Id);
                if (toDelete != null)
                {
                    db.Cedulas.Remove(toDelete);
                    await db.SaveChangesAsync();
                    await LoadAsync();
                }
            }
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }
    }
}
