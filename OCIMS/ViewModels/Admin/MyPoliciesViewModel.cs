using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Services;
using eSureHi.Models;

namespace eSureHi.ViewModels.Admin
{
    public class MyPoliciesViewModel : ObservableObject
    {
        public ObservableCollection<EmployeePolicy> Policies { get; } = new();

        private bool _isLoading;
        private bool _hasData;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public bool HasData { get => _hasData; set => SetProperty(ref _hasData, value); }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        public MyPoliciesViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
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
                var list = await db.EmployeePolicies
                    .Include(ep => ep.Policy)
                    .Where(ep => ep.EmpId == empId)
                    .OrderByDescending(ep => ep.StartDate)
                    .ToListAsync();

                Policies.Clear();
                foreach (var p in list) Policies.Add(p);
                HasData = Policies.Count > 0;
            }
            catch (Exception ex)
            {
                App.ReportError("Load My Policies Failed", ex);
            }
            finally { IsLoading = false; }
        }
    }
}
