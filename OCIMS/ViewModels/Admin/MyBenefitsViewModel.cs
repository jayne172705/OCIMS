using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;

namespace eSureHi.ViewModels.Admin
{
    public class MyBenefitsViewModel : ObservableObject
    {
        public ObservableCollection<Benefit> Benefits { get; } = new();

        private bool _isLoading;
        private bool _hasData;
        private decimal _totalMax;
        private decimal _totalUsed;
        private decimal _totalRemaining;

        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public bool HasData { get => _hasData; set => SetProperty(ref _hasData, value); }
        public decimal TotalMax { get => _totalMax; set => SetProperty(ref _totalMax, value); }
        public decimal TotalUsed { get => _totalUsed; set => SetProperty(ref _totalUsed, value); }
        public decimal TotalRemaining { get => _totalRemaining; set => SetProperty(ref _totalRemaining, value); }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        public MyBenefitsViewModel()
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
                var list = await db.Benefits
                    .Include(b => b.EmployeePolicy)
                        .ThenInclude(ep => ep!.Policy)
                    .Where(b => b.EmployeePolicy!.EmpId == empId)
                    .OrderBy(b => b.YearPeriod)
                    .ThenBy(b => b.BenefitType)
                    .ToListAsync();

                Benefits.Clear();
                foreach (var b in list) Benefits.Add(b);
                HasData = Benefits.Count > 0;
                TotalMax = Benefits.Sum(b => b.MaxBenefit);
                TotalUsed = Benefits.Sum(b => b.UsedBenefit);
                TotalRemaining = Benefits.Sum(b => b.Remaining);
            }
            catch (Exception ex)
            {
                App.ReportError("Load My Benefits Failed", ex);
            }
            finally { IsLoading = false; }
        }
    }
}
