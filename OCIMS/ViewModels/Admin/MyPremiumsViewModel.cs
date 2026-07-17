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
    public class MyPremiumsViewModel : ObservableObject
    {
        public ObservableCollection<Premium> Premiums { get; } = new();

        private bool _isLoading;
        private bool _hasData;
        private decimal _totalDue;
        private decimal _totalPaid;
        private decimal _totalBalance;

        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public bool HasData { get => _hasData; set => SetProperty(ref _hasData, value); }
        public decimal TotalDue { get => _totalDue; set => SetProperty(ref _totalDue, value); }
        public decimal TotalPaid { get => _totalPaid; set => SetProperty(ref _totalPaid, value); }
        public decimal TotalBalance { get => _totalBalance; set => SetProperty(ref _totalBalance, value); }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        public MyPremiumsViewModel()
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
                var list = await db.Premiums
                    .Include(p => p.EmployeePolicy)
                        .ThenInclude(ep => ep!.Policy)
                    .Where(p => p.EmployeePolicy!.EmpId == empId)
                    .OrderByDescending(p => p.BillingMonth)
                    .ToListAsync();

                Premiums.Clear();
                foreach (var p in list) Premiums.Add(p);
                HasData = Premiums.Count > 0;
                TotalDue = Premiums.Sum(p => p.TotalAmount);
                TotalPaid = Premiums.Sum(p => p.AmountPaid);
                TotalBalance = Premiums.Sum(p => p.Balance);
            }
            catch (Exception ex)
            {
                App.ReportError("Load My Premiums Failed", ex);
            }
            finally { IsLoading = false; }
        }
    }
}
