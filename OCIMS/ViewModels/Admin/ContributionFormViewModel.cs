using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;

namespace eSureHi.ViewModels.Admin
{
    public class ContributionFormViewModel : ObservableObject
    {
        private readonly int _empId;
        private readonly int? _editContributionId;

        private decimal _amount;
        private DateTime? _date = DateTime.Today;
        private string _orNumber = string.Empty;
        private string _remarks = string.Empty;

        public decimal Amount { get => _amount; set => SetProperty(ref _amount, value); }
        public DateTime? Date { get => _date; set => SetProperty(ref _date, value); }
        public string OrNumber { get => _orNumber; set => SetProperty(ref _orNumber, value); }
        public string Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }

        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }
        public Action? CloseAction { get; set; }
        public Action? OnSaveSuccess { get; set; }
        public bool IsEditMode => _editContributionId.HasValue;
        public string DialogTitle => IsEditMode ? "Edit Contribution" : "Add Contribution";

        public ContributionFormViewModel(int empId)
        {
            _empId = empId;
            SaveCommand = new RelayCommand(async () => await SaveAsync());
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
        }

        public ContributionFormViewModel(int empId, int contributionId)
            : this(empId)
        {
            _editContributionId = contributionId;
            OnPropertyChanged(nameof(IsEditMode));
            OnPropertyChanged(nameof(DialogTitle));
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var c = await db.Contributions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == _editContributionId && x.EmployeeId == _empId);
                if (c is null) return;

                Amount = c.Amount;
                Date = c.Date.ToDateTime(TimeOnly.MinValue);
                OrNumber = c.OrNumber ?? string.Empty;
                Remarks = c.Remarks ?? string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load failed: {ex.Message}");
            }
        }

        private async Task SaveAsync()
        {
            if (Amount <= 0) { MessageBox.Show("Amount must be greater than zero."); return; }
            if (Date == null) { MessageBox.Show("Date is required."); return; }

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var c = IsEditMode
                    ? await db.Contributions.FirstOrDefaultAsync(x => x.Id == _editContributionId && x.EmployeeId == _empId)
                    : new Contribution { EmployeeId = _empId };

                if (c is null)
                {
                    MessageBox.Show("Contribution record was not found.");
                    return;
                }

                c.Amount = Amount;
                c.Date = DateOnly.FromDateTime(Date.Value);
                c.OrNumber = OrNumber?.Trim() ?? string.Empty;
                c.Remarks = Remarks?.Trim() ?? string.Empty;

                if (!IsEditMode)
                {
                    db.Contributions.Add(c);
                }

                await db.SaveChangesAsync();

                OnSaveSuccess?.Invoke();
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed: {ex.Message}");
            }
        }
    }
}
