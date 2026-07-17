using System;
using System.Collections.ObjectModel;
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
    public class EmployeesViewModel : ObservableObject
    {
        // ── Collections ────────────────────────────────────────────────
        private ObservableCollection<Employee> _allEmployees = new();
        public ObservableCollection<Employee> DisplayedEmployees { get; } = new();
        public ObservableCollection<Department> Departments { get; } = new();

        // ── Selected ───────────────────────────────────────────────────
        private Employee? _selectedEmployee;
        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                SetProperty(ref _selectedEmployee, value);
                OnPropertyChanged(nameof(HasSelection));
                EditCommand.RaiseCanExecuteChanged();
                ViewSummaryCommand.RaiseCanExecuteChanged();
                DeactivateCommand.RaiseCanExecuteChanged();
                CreateAccountCommand.RaiseCanExecuteChanged();
                ManageAccessCommand.RaiseCanExecuteChanged();
            }
        }
        public bool HasSelection => SelectedEmployee is not null;
        public bool HasDeleteSelection => DisplayedEmployees.Any(e => e.IsSelectedForDeletion);
        public int DeleteSelectionCount => DisplayedEmployees.Count(e => e.IsSelectedForDeletion);

        public bool SelectAllDisplayed
        {
            get => DisplayedEmployees.Any() && DisplayedEmployees.All(e => e.IsSelectedForDeletion);
            set
            {
                foreach (var employee in DisplayedEmployees)
                    employee.IsSelectedForDeletion = value;

                RefreshDeleteSelectionState();
            }
        }

        // ── Search + Filter ────────────────────────────────────────────
        private string _searchText = string.Empty;
        private string _statusFilter = "All";
        private Department? _deptFilter;

        public string SearchText
        {
            get => _searchText;
            set { SetProperty(ref _searchText, value); ApplyFilter(); }
        }
        public string StatusFilter
        {
            get => _statusFilter;
            set { SetProperty(ref _statusFilter, value); ApplyFilter(); }
        }
        public Department? DeptFilter
        {
            get => _deptFilter;
            set { SetProperty(ref _deptFilter, value); ApplyFilter(); }
        }

        public string[] StatusOptions { get; } =
            { "All", "Active", "Inactive", "Retired", "Resigned", "Terminated" };

        // ── Counts ─────────────────────────────────────────────────────
        private int _totalCount;
        private int _filteredCount;
        public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
        public int FilteredCount { get => _filteredCount; set => SetProperty(ref _filteredCount, value); }

        // ── State ──────────────────────────────────────────────────────
        private bool _isLoading;
        private string _statusMessage = string.Empty;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand AddCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand ViewSummaryCommand { get; }
        public RelayCommand DeactivateCommand { get; }
        public RelayCommand DeleteSelectedCommand { get; }
        public RelayCommand<Employee> DeleteEmployeeCommand { get; }
        public RelayCommand RefreshDeleteSelectionCommand { get; }
        public RelayCommand ClearFilterCommand { get; }
        public RelayCommand CreateAccountCommand { get; }
        public RelayCommand ManageAccessCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        // ── Constructor ────────────────────────────────────────────────
        public EmployeesViewModel()
        {
            AddCommand = new RelayCommand(OpenAddDialogAsync);
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            EditCommand = new RelayCommand(OpenEditDialog,
                                    () => SelectedEmployee is not null);
            ViewSummaryCommand = new RelayCommand(OpenSummaryDialog,
                                    () => SelectedEmployee is not null);
            DeactivateCommand = new RelayCommand(async () => await DeactivateAsync(),
                                    () => SelectedEmployee is not null &&
                                          SelectedEmployee.EmploymentStatus == "Active");
            DeleteSelectedCommand = new RelayCommand(async () => await DeleteSelectedAsync(),
                                    () => HasDeleteSelection && !IsLoading);
            DeleteEmployeeCommand = new RelayCommand<Employee>(
                                    async employee => await DeleteOneAsync(employee),
                                    employee => employee is not null && !IsLoading);
            RefreshDeleteSelectionCommand = new RelayCommand(RefreshDeleteSelectionState);
            ClearFilterCommand = new RelayCommand(ClearFilters);
            CreateAccountCommand = new RelayCommand(OpenCreateAccountDialog,
                           () => SelectedEmployee is not null);
            ManageAccessCommand = new RelayCommand(async () => await OpenManageAccessDialogAsync(),
                           () => SelectedEmployee is not null);
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);

            _ = LoadAsync();
        }

        // ── Load ───────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                var employees = await db.Employees
                    .Include(e => e.Department)
                    .OrderBy(e => e.LastName)
                    .ThenBy(e => e.FirstName)
                    .ToListAsync();

                var depts = await db.Departments
                    .Where(d => d.IsActive)
                    .OrderBy(d => d.DeptName)
                    .ToListAsync();

                _allEmployees.Clear();
                foreach (var e in employees)
                {
                    e.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName == nameof(Employee.IsSelectedForDeletion))
                            RefreshDeleteSelectionState();
                    };
                    _allEmployees.Add(e);
                }

                Departments.Clear();
                foreach (var d in depts)
                    Departments.Add(d);

                TotalCount = _allEmployees.Count;
                ApplyFilter();
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

        // ── Filter ─────────────────────────────────────────────────────
        private void ApplyFilter()
        {
            var filtered = _allEmployees.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var s = SearchText.Trim().ToLower();
                filtered = filtered.Where(e =>
                    e.FullName.ToLower().Contains(s) ||
                    e.EmployeeNo.ToLower().Contains(s) ||
                    (e.Email?.ToLower().Contains(s) ?? false) ||
                    (e.PositionTitle?.ToLower().Contains(s) ?? false));
            }

            if (StatusFilter != "All")
                filtered = filtered.Where(e => e.EmploymentStatus == StatusFilter);

            if (DeptFilter is not null)
                filtered = filtered.Where(e => e.DeptId == DeptFilter.DeptId);

            DisplayedEmployees.Clear();
            foreach (var e in filtered)
                DisplayedEmployees.Add(e);

            FilteredCount = DisplayedEmployees.Count;
            RefreshDeleteSelectionState();
        }

        private void RefreshDeleteSelectionState()
        {
            OnPropertyChanged(nameof(HasDeleteSelection));
            OnPropertyChanged(nameof(DeleteSelectionCount));
            OnPropertyChanged(nameof(SelectAllDisplayed));
            DeleteSelectedCommand.RaiseCanExecuteChanged();
        }

        // ── Add ────────────────────────────────────────────────────────
        private async Task OpenAddDialogAsync()
        {
            try
            {
                var dialog = new Views.Admin.Dialogs.EmployeeFormDialog();
                await dialog.InitAsync();
                dialog.SetSaveCallback(async () => await LoadAsync());
                if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Open employee form failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Edit ───────────────────────────────────────────────────────
        private void OpenEditDialog()
        {
            if (SelectedEmployee is null) return;
            var dialog = new Views.Admin.Dialogs.EmployeeFormDialog(SelectedEmployee.EmpId);
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        // ── View Summary ───────────────────────────────────────────────
        private void OpenSummaryDialog()
        {
            if (SelectedEmployee is null) return;
            var dialog = new Views.Admin.Dialogs.EmployeeSummaryDialog(SelectedEmployee.EmpId);
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        // ── Deactivate (soft delete) ───────────────────────────────────
        private async Task DeactivateAsync()
        {
            if (SelectedEmployee is null) return;

            var result = MessageBox.Show(
                $"Deactivate {SelectedEmployee.FullName}?\n\nThis will set their status to Inactive. " +
                "Their records will be preserved.",
                "Confirm Deactivate",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var emp = await db.Employees.FindAsync(SelectedEmployee.EmpId);
                if (emp is null) return;

                emp.EmploymentStatus = "Inactive";
                emp.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Deactivate failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Clear Filters ──────────────────────────────────────────────
        private void ClearFilters()
        {
            _searchText = string.Empty;
            _statusFilter = "All";
            _deptFilter = null;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(StatusFilter));
            OnPropertyChanged(nameof(DeptFilter));
            ApplyFilter();
        }

        // ── Create User Account ────────────────────────────────────────
        private void OpenCreateAccountDialog()
        {
            if (SelectedEmployee is null) return;
            var dialog = new Views.Admin.Dialogs.CreateUserAccountDialog(
                SelectedEmployee.EmpId,
                SelectedEmployee.FullName);
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        // ── Navigation ─────────────────────────────────────────────────
        private async Task OpenManageAccessDialogAsync()
        {
            if (SelectedEmployee is null) return;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var user = await db.SystemUsers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.EmpId == SelectedEmployee.EmpId);

                if (user is null)
                {
                    MessageBox.Show(
                        "This employee does not have a user account yet.",
                        "Manage Access",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                if (!PermissionService.IsControllableRole(user.Role))
                {
                    MessageBox.Show(
                        $"{user.Role} accounts are exempt from per-user menu restrictions.",
                        "Manage Access",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var dialog = new Views.Admin.Dialogs.UserPermissionsDialog(user);
                dialog.SetSaveCallback(async () => await LoadAsync());
                if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Open access settings failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteOneAsync(Employee? employee)
        {
            if (employee is null) return;

            await DeleteEmployeesAsync(new[] { employee },
                $"Permanently delete {employee.FullName}?\n\nThis will delete the employee and related database records.");
        }

        private async Task DeleteSelectedAsync()
        {
            var employees = DisplayedEmployees
                .Where(e => e.IsSelectedForDeletion)
                .ToList();

            if (!employees.Any()) return;

            await DeleteEmployeesAsync(employees,
                $"Permanently delete {employees.Count} selected employee(s)?\n\nThis will delete their related database records too.");
        }

        private async Task DeleteEmployeesAsync(IEnumerable<Employee> employees, string prompt)
        {
            var employeeList = employees.ToList();
            if (!employeeList.Any()) return;
            var deleteCount = employeeList.Count;

            var result = MessageBox.Show(
                prompt + "\n\nThis cannot be undone.",
                "Confirm Permanent Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            IsLoading = true;
            try
            {
                await using var strategyDb = eSureHiDbContextFactory.Create();
                var strategy = strategyDb.Database.CreateExecutionStrategy();

                await strategy.ExecuteAsync(async () =>
                {
                    await using var db = eSureHiDbContextFactory.Create();
                    await using var transaction = await db.Database.BeginTransactionAsync();

                    var employeeIds = employeeList.Select(e => e.EmpId).Distinct().ToList();
                    var employeePolicyIds = await db.EmployeePolicies
                        .Where(ep => employeeIds.Contains(ep.EmpId))
                        .Select(ep => ep.EpId)
                        .ToListAsync();
                    var beneficiaryIds = await db.Beneficiaries
                        .Where(b => employeeIds.Contains(b.EmpId))
                        .Select(b => b.BenId)
                        .ToListAsync();
                    var claimIds = await db.Claims
                        .Where(c => employeeIds.Contains(c.EmpId) ||
                                    (c.BenId.HasValue && beneficiaryIds.Contains(c.BenId.Value)))
                        .Select(c => c.ClaimId)
                        .ToListAsync();
                    var documentIds = await db.Documents
                        .Where(d => employeeIds.Contains(d.EmpId))
                        .Select(d => d.DocumentId)
                        .ToListAsync();
                    var userIds = await db.SystemUsers
                        .Where(u => u.EmpId.HasValue && employeeIds.Contains(u.EmpId.Value))
                        .Select(u => u.UserId)
                        .ToListAsync();

                    await db.Notifications
                        .Where(n => userIds.Contains(n.RecipientId))
                        .ExecuteDeleteAsync();
                    await db.UserPermissions
                        .Where(p => userIds.Contains(p.UserId))
                        .ExecuteDeleteAsync();
                    await db.ClaimDocuments
                        .Where(d => claimIds.Contains(d.ClaimId))
                        .ExecuteDeleteAsync();
                    await db.Claims
                        .Where(c => claimIds.Contains(c.ClaimId))
                        .ExecuteDeleteAsync();
                    await db.DocumentTransactions
                        .Where(t => t.DocumentId.HasValue && documentIds.Contains(t.DocumentId.Value))
                        .ExecuteDeleteAsync();
                    await db.Documents
                        .Where(d => documentIds.Contains(d.DocumentId))
                        .ExecuteDeleteAsync();
                    await db.Cedulas
                        .Where(c => employeeIds.Contains(c.EmployeeId))
                        .ExecuteDeleteAsync();
                    await db.Contributions
                        .Where(c => employeeIds.Contains(c.EmployeeId))
                        .ExecuteDeleteAsync();
                    await db.Trainings
                        .Where(t => employeeIds.Contains(t.EmployeeId))
                        .ExecuteDeleteAsync();
                    await db.Benefits
                        .Where(b => employeePolicyIds.Contains(b.EpId))
                        .ExecuteDeleteAsync();
                    await db.Premiums
                        .Where(p => employeePolicyIds.Contains(p.EpId))
                        .ExecuteDeleteAsync();
                    await db.EmployeePolicies
                        .Where(ep => employeePolicyIds.Contains(ep.EpId))
                        .ExecuteDeleteAsync();
                    await db.BeneficiaryStaging
                        .Where(b => (b.LinkedEmpId.HasValue && employeeIds.Contains(b.LinkedEmpId.Value)) ||
                                    (b.LinkedBenId.HasValue && beneficiaryIds.Contains(b.LinkedBenId.Value)))
                        .ExecuteDeleteAsync();
                    await db.Beneficiaries
                        .Where(b => beneficiaryIds.Contains(b.BenId))
                        .ExecuteDeleteAsync();
                    await db.SystemUsers
                        .Where(u => userIds.Contains(u.UserId))
                        .ExecuteDeleteAsync();
                    await db.Employees
                        .Where(e => employeeIds.Contains(e.EmpId))
                        .ExecuteDeleteAsync();

                    await transaction.CommitAsync();
                });

                StatusMessage = $"Deleted {deleteCount} employee(s).";
                SelectedEmployee = null;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delete failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }
    }
}
