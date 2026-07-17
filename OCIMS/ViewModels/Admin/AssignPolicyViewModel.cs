using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Models;
using eSureHi.Helpers;

namespace eSureHi.ViewModels.Admin
{
    public class AssignPolicyViewModel : ObservableObject
    {
        private ObservableCollection<Employee> _allEmployees = new();
        private Employee? _selectedEmployee;
        private string _searchText = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isBusy;
        private int _policyId;
        private string _policyName = string.Empty;
        private string _policyType = string.Empty;
        private DateTime? _startDate = DateTime.Today;
        private DateTime? _endDate;
        private string _assignmentStatus = "Active";
        private string _remarks = string.Empty;
        private decimal _coverageLimit;
        private decimal _employeeShare;
        private decimal _employerShare;
        private string _dialogTitle = "Assign Policy to Employee";

        public string DialogTitle
        {
            get => _dialogTitle;
            set => SetProperty(ref _dialogTitle, value);
        }

        public ObservableCollection<Employee> Employees
        {
            get => _allEmployees;
            set
            {
                if (SetProperty(ref _allEmployees, value))
                {
                    FilterEmployees();
                }
            }
        }

        public ObservableCollection<Employee> FilteredEmployees { get; } = new();

        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                if (SetProperty(ref _selectedEmployee, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                    AssignPolicyCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool HasSelection => SelectedEmployee is not null;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterEmployees();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    AssignPolicyCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsLoading
        {
            get => IsBusy;
            set => IsBusy = value;
        }

        public string PolicyName
        {
            get => _policyName;
            set => SetProperty(ref _policyName, value);
        }

        public string PolicyType
        {
            get => _policyType;
            set => SetProperty(ref _policyType, value);
        }

        public DateTime? StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        public DateTime? EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        public string AssignmentStatus
        {
            get => _assignmentStatus;
            set => SetProperty(ref _assignmentStatus, value);
        }

        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        public decimal CoverageLimit
        {
            get => _coverageLimit;
            set => SetProperty(ref _coverageLimit, value);
        }

        public decimal EmployeeShare
        {
            get => _employeeShare;
            set => SetProperty(ref _employeeShare, value);
        }

        public decimal EmployerShare
        {
            get => _employerShare;
            set => SetProperty(ref _employerShare, value);
        }

        public ObservableCollection<string> StatusOptions { get; } =
            new(new[] { "Active", "Inactive", "Pending", "Expired" });

        public RelayCommand AssignPolicyCommand { get; }
        public RelayCommand SaveCommand => AssignPolicyCommand;
        public RelayCommand CancelCommand { get; }
        public Action? CloseAction { get; set; }
        public Action? OnSaveSuccess { get; set; }

        public AssignPolicyViewModel(int policyId, string policyName)
        {
            _policyId = policyId;
            _policyName = policyName;
            PolicyName = policyName;
            DialogTitle = $"Assign {policyName}";

            AssignPolicyCommand = new RelayCommand(
                async () => await AssignPolicyAsync(),
                CanAssignPolicy);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());

            _ = LoadDataAsync();
        }

        private bool CanAssignPolicy()
        {
            return SelectedEmployee is not null && !IsBusy;
        }

        private async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                using var db = eSureHiDbContextFactory.Create();

                var policy = await db.InsurancePolicies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PolicyId == _policyId);

                if (policy is null)
                {
                    ErrorMessage = "Policy not found.";
                    return;
                }

                PolicyName = policy.PolicyName;
                PolicyType = policy.PolicyType;
                CoverageLimit = policy.CoverageAmount;
                DialogTitle = $"Assign {policy.PolicyName} ({policy.PolicyType})";

                var employees = await db.Employees
                    .Where(e => e.EmploymentStatus == "Active")
                    .Where(e => e.EmploymentType == policy.PolicyType)
                    .OrderBy(e => e.LastName)
                    .ThenBy(e => e.FirstName)
                    .ToListAsync();

                Employees = new ObservableCollection<Employee>(employees);
                ErrorMessage = employees.Count == 0
                    ? $"No active {policy.PolicyType} employees available for this policy."
                    : string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error loading employees: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void FilterEmployees()
        {
            FilteredEmployees.Clear();

            var filtered = Employees.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.Trim().ToLower();
                filtered = filtered.Where(e =>
                    (e.FullName?.ToLower().Contains(search) ?? false) ||
                    (e.EmployeeNo?.ToLower().Contains(search) ?? false) ||
                    (e.EmploymentType?.ToLower().Contains(search) ?? false));
            }

            foreach (var employee in filtered)
            {
                FilteredEmployees.Add(employee);
            }
        }

        private async Task AssignPolicyAsync()
        {
            if (SelectedEmployee is null)
            {
                ErrorMessage = "Please select an employee.";
                return;
            }

            if (StartDate is null)
            {
                ErrorMessage = "Please select a start date.";
                return;
            }

            if (EndDate.HasValue && EndDate.Value.Date < StartDate.Value.Date)
            {
                ErrorMessage = "End date cannot be earlier than start date.";
                return;
            }

            try
            {
                IsBusy = true;
                using var db = eSureHiDbContextFactory.Create();

                var policy = await db.InsurancePolicies
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.PolicyId == _policyId);

                if (policy is null)
                {
                    ErrorMessage = "Policy not found.";
                    return;
                }

                var employee = await db.Employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.EmpId == SelectedEmployee.EmpId);

                if (employee is null)
                {
                    ErrorMessage = "Employee not found.";
                    return;
                }

                if (!string.Equals(employee.EmploymentType, policy.PolicyType, StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage = $"{employee.FullName} is {employee.EmploymentType}. Assign the {employee.EmploymentType} policy only.";
                    return;
                }

                var existingAssignment = await db.EmployeePolicies
                    .FirstOrDefaultAsync(ep =>
                        ep.EmpId == SelectedEmployee.EmpId &&
                        ep.PolicyId == _policyId);

                if (existingAssignment is not null)
                {
                    ErrorMessage = "This employee already has this policy assigned.";
                    return;
                }

                var assignment = new EmployeePolicy
                {
                    EmpId = SelectedEmployee.EmpId,
                    PolicyId = _policyId,
                    CoverageLimit = CoverageLimit,
                    EmployeeShare = EmployeeShare,
                    EmployerShare = EmployerShare,
                    StartDate = StartDate.HasValue
                        ? DateOnly.FromDateTime(StartDate.Value)
                        : DateOnly.FromDateTime(DateTime.Today),
                    EndDate = EndDate.HasValue
                        ? DateOnly.FromDateTime(EndDate.Value)
                        : null,
                    AssignmentStatus = string.IsNullOrWhiteSpace(AssignmentStatus)
                        ? "Active"
                        : AssignmentStatus.Trim(),
                    Remarks = string.IsNullOrWhiteSpace(Remarks)
                        ? null
                        : Remarks.Trim(),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                db.EmployeePolicies.Add(assignment);
                await db.SaveChangesAsync();

                ErrorMessage = string.Empty;
                MessageBox.Show($"Policy successfully assigned to {SelectedEmployee.FullName}.", "Assignment Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                
                OnSaveSuccess?.Invoke();
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error assigning policy: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
