using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;

namespace eSureHi.ViewModels.Admin
{
    public class EmployeeSummaryViewModel : ObservableObject
    {
        private readonly int _empId;

        // ── Details ────────────────────────────────────────────────────
        private string _fullName = string.Empty;
        private string _employeeNo = string.Empty;
        private string _department = string.Empty;
        private string _position = string.Empty;
        private decimal _totalContributions;
        private int _totalTrainings;

        public string FullName { get => _fullName; set => SetProperty(ref _fullName, value); }
        public string EmployeeNo { get => _employeeNo; set => SetProperty(ref _employeeNo, value); }
        public string Department { get => _department; set => SetProperty(ref _department, value); }
        public string Position { get => _position; set => SetProperty(ref _position, value); }
        public decimal TotalContributions { get => _totalContributions; set => SetProperty(ref _totalContributions, value); }
        public int TotalTrainings { get => _totalTrainings; set => SetProperty(ref _totalTrainings, value); }

        // ── Collections ────────────────────────────────────────────────
        public ObservableCollection<Contribution> Contributions { get; } = new();
        public ObservableCollection<Training> Trainings { get; } = new();
        public ObservableCollection<EmployeePolicy> AssignedPolicies { get; } = new();

        // ── Selected Items ─────────────────────────────────────────────
        private Contribution? _selectedContribution;
        public Contribution? SelectedContribution
        {
            get => _selectedContribution;
            set
            {
                SetProperty(ref _selectedContribution, value);
                EditContributionCommand.RaiseCanExecuteChanged();
                DeleteContributionCommand.RaiseCanExecuteChanged();
            }
        }

        private Training? _selectedTraining;
        public Training? SelectedTraining
        {
            get => _selectedTraining;
            set { SetProperty(ref _selectedTraining, value); DeleteTrainingCommand.RaiseCanExecuteChanged(); }
        }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand LoadCommand { get; }
        public RelayCommand AddContributionCommand { get; }
        public RelayCommand EditContributionCommand { get; }
        public RelayCommand DeleteContributionCommand { get; }
        public RelayCommand AddTrainingCommand { get; }
        public RelayCommand DeleteTrainingCommand { get; }
        public RelayCommand CloseCommand { get; }
        public Action? CloseAction { get; set; }

        public EmployeeSummaryViewModel(int empId)
        {
            _empId = empId;
            LoadCommand = new RelayCommand(async () => await LoadAsync());
            AddContributionCommand = new RelayCommand(AddContribution);
            EditContributionCommand = new RelayCommand(EditContribution, () => SelectedContribution != null);
            DeleteContributionCommand = new RelayCommand(async () => await DeleteContributionAsync(), () => SelectedContribution != null);
            AddTrainingCommand = new RelayCommand(AddTraining);
            DeleteTrainingCommand = new RelayCommand(async () => await DeleteTrainingAsync(), () => SelectedTraining != null);
            CloseCommand = new RelayCommand(() => CloseAction?.Invoke());
        }

        public async Task LoadAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var emp = await db.Employees
                    .Include(e => e.Department)
                    .Include(e => e.Contributions)
                    .Include(e => e.Trainings)
                    .Include(e => e.EmployeePolicies)
                        .ThenInclude(ep => ep.Policy)
                    .FirstOrDefaultAsync(e => e.EmpId == _empId);

                if (emp == null) return;

                FullName = emp.FullName;
                EmployeeNo = emp.EmployeeNo;
                Department = emp.Department?.DeptName ?? "N/A";
                Position = emp.PositionTitle ?? "N/A";

                Contributions.Clear();
                foreach (var c in emp.Contributions.OrderByDescending(x => x.Date))
                    Contributions.Add(c);

                Trainings.Clear();
                foreach (var t in emp.Trainings.OrderByDescending(x => x.DateAttended))
                    Trainings.Add(t);

                AssignedPolicies.Clear();
                foreach (var p in emp.EmployeePolicies.OrderByDescending(x => x.StartDate))
                    AssignedPolicies.Add(p);

                TotalContributions = emp.Contributions.Sum(c => c.Amount);
                TotalTrainings = emp.Trainings.Count;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Loading Summary");
            }
        }

        private void AddContribution()
        {
            var dialog = new Views.Admin.Dialogs.ContributionFormDialog(_empId);
            if (App.ActiveShell != null) dialog.Owner = App.ActiveShell;
            
            if (dialog.ShowDialog() == true)
            {
                _ = LoadAsync(); // Refresh list
            }
        }

        private void EditContribution()
        {
            if (SelectedContribution is null) return;
            var dialog = new Views.Admin.Dialogs.ContributionFormDialog(_empId, SelectedContribution.Id);
            if (App.ActiveShell != null) dialog.Owner = App.ActiveShell;

            if (dialog.ShowDialog() == true)
            {
                _ = LoadAsync();
            }
        }

        private async Task DeleteContributionAsync()
        {
            if (SelectedContribution == null) return;
            if (MessageBox.Show("Are you sure you want to delete this contribution?", "Confirm Delete", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var toDelete = await db.Contributions.FindAsync(SelectedContribution.Id);
                if (toDelete != null)
                {
                    db.Contributions.Remove(toDelete);
                    await db.SaveChangesAsync();
                    await LoadAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Deleting");
            }
        }

        private void AddTraining()
        {
            var dialog = new Views.Admin.Dialogs.TrainingFormDialog(_empId);
            if (App.ActiveShell != null) dialog.Owner = App.ActiveShell;

            if (dialog.ShowDialog() == true)
            {
                _ = LoadAsync(); // Refresh list
            }
        }

        private async Task DeleteTrainingAsync()
        {
            if (SelectedTraining == null) return;
            if (MessageBox.Show("Are you sure you want to delete this training record?", "Confirm Delete", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var toDelete = await db.Trainings.FindAsync(SelectedTraining.Id);
                if (toDelete != null)
                {
                    db.Trainings.Remove(toDelete);
                    await db.SaveChangesAsync();
                    await LoadAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Deleting");
            }
        }
    }
}
