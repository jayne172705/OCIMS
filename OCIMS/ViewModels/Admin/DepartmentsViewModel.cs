using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace eSureHi.ViewModels.Admin
{
    public class DepartmentsViewModel : ObservableObject
    {
        public ObservableCollection<Department> Departments { get; } = new();

        private Department? _selected;
        public Department? Selected
        {
            get => _selected;
            set
            {
                SetProperty(ref _selected, value);
                if (value is not null)
                {
                    EditName = value.DeptName;
                    EditCode = value.DeptCode;
                    EditDescription = value.Description ?? string.Empty;
                }
                OnPropertyChanged(nameof(HasSelection));
                SaveEditCommand.RaiseCanExecuteChanged();
                DeactivateCommand.RaiseCanExecuteChanged();
            }
        }
        public bool HasSelection => Selected is not null;

        // ── New ────────────────────────────────────────────────────────
        private string _newName = string.Empty;
        private string _newCode = string.Empty;
        private string _newDescription = string.Empty;

        public string NewName
        {
            get => _newName;
            set => SetProperty(ref _newName, value);
        }
        public string NewCode
        {
            get => _newCode;
            set => SetProperty(ref _newCode, value);
        }
        public string NewDescription
        {
            get => _newDescription;
            set => SetProperty(ref _newDescription, value);
        }

        // ── Edit ───────────────────────────────────────────────────────
        private string _editName = string.Empty;
        private string _editCode = string.Empty;
        private string _editDescription = string.Empty;

        public string EditName
        {
            get => _editName;
            set => SetProperty(ref _editName, value);
        }
        public string EditCode
        {
            get => _editCode;
            set => SetProperty(ref _editCode, value);
        }
        public string EditDescription
        {
            get => _editDescription;
            set => SetProperty(ref _editDescription, value);
        }

        // ── State ──────────────────────────────────────────────────────
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;

        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand AddCommand { get; }
        public RelayCommand SaveEditCommand { get; }
        public RelayCommand DeactivateCommand { get; }
        public RelayCommand CloseCommand { get; }

        public Action? CloseAction { get; set; }

        public DepartmentsViewModel()
        {
            AddCommand = new RelayCommand(async () => await AddAsync());
            SaveEditCommand = new RelayCommand(async () => await SaveEditAsync(),
                                    () => Selected is not null);
            DeactivateCommand = new RelayCommand(async () => await DeactivateAsync(),
                                    () => Selected is not null);
            CloseCommand = new RelayCommand(() => CloseAction?.Invoke());
            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var list = await db.Departments
                    .Where(d => d.IsActive)
                    .OrderBy(d => d.DeptName)
                    .ToListAsync();
                Departments.Clear();
                foreach (var d in list) Departments.Add(d);
            }
            catch (Exception ex) { ErrorMessage = $"Load failed: {ex.Message}"; }
        }

        private async Task AddAsync()
        {
            if (string.IsNullOrWhiteSpace(NewName))
            { ErrorMessage = "Department name is required."; return; }
            if (string.IsNullOrWhiteSpace(NewCode))
            { ErrorMessage = "Department code is required."; return; }

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                bool exists = await db.Departments
                    .AnyAsync(d => d.DeptCode == NewCode.Trim().ToUpper());
                if (exists)
                { ErrorMessage = "Department code already exists."; return; }

                var dept = new Department
                {
                    DeptName = NewName.Trim(),
                    DeptCode = NewCode.Trim().ToUpper(),
                    Description = string.IsNullOrWhiteSpace(NewDescription)
                                      ? null : NewDescription.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                db.Departments.Add(dept);
                await db.SaveChangesAsync();
                await AuditService.LogInsert("departments", dept.DeptId,
                    $"Department added: {NewName}");

                NewName = string.Empty;
                NewCode = string.Empty;
                NewDescription = string.Empty;
                ErrorMessage = string.Empty;
                StatusMessage = "Department added.";
                await LoadAsync();
            }
            catch (Exception ex) { ErrorMessage = $"Failed: {ex.Message}"; }
        }

        private async Task SaveEditAsync()
        {
            if (Selected is null) return;
            if (string.IsNullOrWhiteSpace(EditName))
            { ErrorMessage = "Name is required."; return; }
            if (string.IsNullOrWhiteSpace(EditCode))
            { ErrorMessage = "Code is required."; return; }

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var d = await db.Departments.FindAsync(Selected.DeptId);
                if (d is null) return;
                d.DeptName = EditName.Trim();
                d.DeptCode = EditCode.Trim().ToUpper();
                d.Description = string.IsNullOrWhiteSpace(EditDescription)
                                    ? null : EditDescription.Trim();
                d.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();

                // After SaveEditAsync:
                await AuditService.LogUpdate("departments", Selected.DeptId,
                    $"Department updated: {EditName}");

                StatusMessage = "Changes saved.";
                ErrorMessage = string.Empty;
                await LoadAsync();
            }
            catch (Exception ex) { ErrorMessage = $"Failed: {ex.Message}"; }
        }

        private async Task DeactivateAsync()
        {
            if (Selected is null) return;

            // Block if employees are assigned
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                bool hasEmployees = await db.Employees
                    .AnyAsync(e => e.DeptId == Selected.DeptId &&
                                   e.EmploymentStatus == "Active");
                if (hasEmployees)
                {
                    MessageBox.Show(
                        "Cannot deactivate a department that has active employees.",
                        "Cannot Deactivate",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"Deactivate '{Selected.DeptName}'?",
                    "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;

                var d = await db.Departments.FindAsync(Selected.DeptId);
                if (d is null) return;
                d.IsActive = false;
                d.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();

                // After DeactivateAsync:
                await AuditService.LogUpdate("departments", Selected.DeptId,
                    $"Department deactivated: {Selected.DeptName}");

                StatusMessage = "Department deactivated.";
                await LoadAsync();
            }
            catch (Exception ex) { ErrorMessage = $"Failed: {ex.Message}"; }
        }
    }
}
