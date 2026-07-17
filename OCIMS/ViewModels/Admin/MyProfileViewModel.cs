using System;
using System.IO;
using System.Threading.Tasks;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using Microsoft.EntityFrameworkCore;

namespace eSureHi.ViewModels.Admin
{
    public class MyProfileViewModel : ObservableObject
    {
        private Employee? _employee;
        public Employee? Employee
        {
            get => _employee;
            set
            {
                SetProperty(ref _employee, value);
                OnPropertyChanged(nameof(HasPhoto));
                OnPropertyChanged(nameof(PhotoPath));
                OnPropertyChanged(nameof(DepartmentName));
            }
        }

        public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoPath)
                                        && File.Exists(PhotoPath);
        public string PhotoPath => Employee?.PhotoPath ?? string.Empty;
        public string DepartmentName => Employee?.Department?.DeptName ?? "—";

        private bool _isLoading;
        private string _errorMessage = string.Empty;

        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand ChangePhotoCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        public MyProfileViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            ChangePhotoCommand = new RelayCommand(ChangePhoto);
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

        private void ChangePhoto()
        {
            var path = ProfileImageService.PickAndSaveCurrentProfileImage();
            if (string.IsNullOrWhiteSpace(path)) return;

            if (Employee is not null)
            {
                Employee.PhotoPath = path;
                OnPropertyChanged(nameof(PhotoPath));
                OnPropertyChanged(nameof(HasPhoto));
            }
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var empId = AuthService.Instance.CurrentEmployee?.EmpId;
                if (empId is null) return;

                using var db = eSureHiDbContextFactory.Create();
                Employee = await db.Employees
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.EmpId == empId);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load failed: {ex.Message}";
            }
            finally { IsLoading = false; }
        }
    }
}
