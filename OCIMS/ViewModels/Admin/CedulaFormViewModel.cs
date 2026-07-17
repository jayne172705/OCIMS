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
    public class CedulaFormViewModel : ObservableObject
    {
        private readonly int? _editCedulaId;

        public ObservableCollection<Employee> Employees { get; } = new();

        private Employee? _selectedEmployee;
        private string _cedulaNo = string.Empty;
        private DateTime? _issueDate = DateTime.Today;
        private string _placeIssued = string.Empty;
        private decimal _amountPaid;
        private string _remarks = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isBusy;

        public Employee? SelectedEmployee { get => _selectedEmployee; set => SetProperty(ref _selectedEmployee, value); }
        public string CedulaNo { get => _cedulaNo; set => SetProperty(ref _cedulaNo, value); }
        public DateTime? IssueDate { get => _issueDate; set => SetProperty(ref _issueDate, value); }
        public string PlaceIssued { get => _placeIssued; set => SetProperty(ref _placeIssued, value); }
        public decimal AmountPaid { get => _amountPaid; set => SetProperty(ref _amountPaid, value); }
        public string Remarks { get => _remarks; set => SetProperty(ref _remarks, value); }
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
        public bool IsBusy { get => _isBusy; set { SetProperty(ref _isBusy, value); SaveCommand.RaiseCanExecuteChanged(); } }

        public bool IsEditMode => _editCedulaId.HasValue;
        public string DialogTitle => IsEditMode ? "Edit Cedula" : "Add Cedula";

        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }
        public Action? CloseAction { get; set; }
        public Action? OnSaveSuccess { get; set; }

        public CedulaFormViewModel(int? cedulaId = null)
        {
            _editCedulaId = cedulaId;
            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsBusy);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
            _ = LoadAsync();
        }

        private async Task LoadAsync()
        {
            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var employees = await db.Employees
                    .Where(e => e.EmploymentStatus == "Active")
                    .OrderBy(e => e.LastName)
                    .ThenBy(e => e.FirstName)
                    .ToListAsync();

                Employees.Clear();
                foreach (var employee in employees) Employees.Add(employee);

                if (!IsEditMode) return;

                var cedula = await db.Cedulas
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == _editCedulaId);
                if (cedula is null) return;

                SelectedEmployee = Employees.FirstOrDefault(e => e.EmpId == cedula.EmployeeId);
                CedulaNo = cedula.CedulaNo;
                IssueDate = cedula.IssueDate;
                PlaceIssued = cedula.PlaceIssued;
                AmountPaid = cedula.AmountPaid;
                Remarks = cedula.Remarks;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool Validate()
        {
            if (SelectedEmployee is null) { ErrorMessage = "Please select an employee."; return false; }
            if (string.IsNullOrWhiteSpace(CedulaNo)) { ErrorMessage = "Cedula number is required."; return false; }
            if (IssueDate is null) { ErrorMessage = "Issue date is required."; return false; }
            if (AmountPaid < 0) { ErrorMessage = "Amount paid cannot be negative."; return false; }
            ErrorMessage = string.Empty;
            return true;
        }

        private async Task SaveAsync()
        {
            if (!Validate()) return;

            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var cedula = IsEditMode
                    ? await db.Cedulas.FirstOrDefaultAsync(c => c.Id == _editCedulaId)
                    : new Cedula { CreatedAt = DateTime.Now };

                if (cedula is null)
                {
                    ErrorMessage = "Cedula record was not found.";
                    return;
                }

                cedula.EmployeeId = SelectedEmployee!.EmpId;
                cedula.CedulaNo = CedulaNo.Trim();
                cedula.IssueDate = IssueDate!.Value;
                cedula.PlaceIssued = string.IsNullOrWhiteSpace(PlaceIssued) ? string.Empty : PlaceIssued.Trim();
                cedula.AmountPaid = AmountPaid;
                cedula.Remarks = string.IsNullOrWhiteSpace(Remarks) ? string.Empty : Remarks.Trim();

                if (!IsEditMode)
                {
                    db.Cedulas.Add(cedula);
                }

                await db.SaveChangesAsync();

                // Integrate with Workflow Hub
                if (!IsEditMode)
                {
                    await WorkflowService.UpdateCedulaStatusAsync(
                        cedula.Id, 
                        WorkflowStatuses.Approved, 
                        "Initial recording of cedula payment.");
                }
                else
                {
                    await WorkflowService.UpdateCedulaStatusAsync(
                        cedula.Id, 
                        WorkflowStatuses.UnderReview, 
                        "Cedula record updated/corrected.");
                }

                OnSaveSuccess?.Invoke();
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Save failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
