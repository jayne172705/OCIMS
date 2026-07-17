using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace eSureHi.ViewModels.Admin
{
    public class BenefitFormViewModel : ObservableObject
    {
        public bool IsEditMode { get; private set; }
        public string DialogTitle => IsEditMode ? "Edit Benefit" : "Add Benefit";
        private int _editBenefitId;

        private ObservableCollection<Employee> _allEmployees = new();
        public ObservableCollection<Employee> FilteredEmployees { get; } = new();

        private string _employeeSearch = string.Empty;
        public string EmployeeSearch
        {
            get => _employeeSearch;
            set
            {
                SetProperty(ref _employeeSearch, value);
                FilterEmployees();
            }
        }

        private Employee? _selectedEmployee;
        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                SetProperty(ref _selectedEmployee, value);
                OnPropertyChanged(nameof(HasEmployee));
                SelectedEmployeePolicy = null;
                OnPropertyChanged(nameof(HasPolicies));
                OnPropertyChanged(nameof(PolicyEmptyMessage));
                _ = LoadPoliciesAsync();
            }
        }

        public bool HasEmployee => SelectedEmployee is not null;

        public ObservableCollection<EmployeePolicy> EmployeePolicies { get; } = new();

        private EmployeePolicy? _selectedEmployeePolicy;
        public EmployeePolicy? SelectedEmployeePolicy
        {
            get => _selectedEmployeePolicy;
            set => SetProperty(ref _selectedEmployeePolicy, value);
        }

        public bool HasPolicies => EmployeePolicies.Count > 0;

        private bool _isLoadingPolicies;
        public bool IsLoadingPolicies
        {
            get => _isLoadingPolicies;
            set => SetProperty(ref _isLoadingPolicies, value);
        }

        public string PolicyEmptyMessage =>
            HasEmployee
                ? "No active policy assignment found for this employee."
                : "Select an employee first.";

        private string _benefitType = "Medical";
        private int _yearPeriod = DateTime.Today.Year;
        private decimal _maxBenefit;
        private string _maxBenefitText = string.Empty;
        private string _notes = string.Empty;

        public string BenefitType { get => _benefitType; set => SetProperty(ref _benefitType, value); }
        public int YearPeriod { get => _yearPeriod; set => SetProperty(ref _yearPeriod, value); }
        public decimal MaxBenefit { get => _maxBenefit; set => SetProperty(ref _maxBenefit, value); }
        public string MaxBenefitText
        {
            get => _maxBenefitText;
            set
            {
                if (SetProperty(ref _maxBenefitText, value))
                {
                    MaxBenefit = ParseMoney(value);
                }
            }
        }
        public string Notes { get => _notes; set => SetProperty(ref _notes, value); }

        public string[] BenefitTypes { get; } =
            { "Medical", "Dental", "Vision", "Life", "Accident", "Optical", "Others" };

        public int[] YearOptions { get; } = Enumerable
            .Range(DateTime.Today.Year - 3, 6)
            .ToArray();

        private string _errorMessage = string.Empty;
        private bool _isBusy;

        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                SetProperty(ref _isBusy, value);
                SaveCommand.RaiseCanExecuteChanged();
            }
        }

        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public Action? CloseAction { get; set; }
        public Action? OnSaveSuccess { get; set; }

        public BenefitFormViewModel()
        {
            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsBusy);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
            _ = LoadEmployeesAsync();
        }

        public async Task InitEditAsync(int benefitId)
        {
            IsEditMode = true;
            _editBenefitId = benefitId;
            OnPropertyChanged(nameof(DialogTitle));

            using var db = eSureHiDbContextFactory.Create();
            var benefit = await db.Benefits
                .Include(x => x.EmployeePolicy)
                    .ThenInclude(ep => ep!.Employee)
                .Include(x => x.EmployeePolicy)
                    .ThenInclude(ep => ep!.Policy)
                .FirstOrDefaultAsync(x => x.BenefitId == benefitId);

            if (benefit is null)
                return;

            await LoadEmployeesAsync();
            SelectedEmployee = _allEmployees
                .FirstOrDefault(e => e.EmpId == benefit.EmployeePolicy!.EmpId);

            await LoadPoliciesAsync();
            SelectedEmployeePolicy = EmployeePolicies
                .FirstOrDefault(ep => ep.EpId == benefit.EpId);

            BenefitType = benefit.BenefitType;
            YearPeriod = benefit.YearPeriod ?? DateTime.Today.Year;
            MaxBenefit = benefit.MaxBenefit;
            MaxBenefitText = MaxBenefit.ToString("N2", CultureInfo.InvariantCulture);
            Notes = benefit.Notes ?? string.Empty;
        }

        private async Task LoadEmployeesAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var list = await db.Employees
                    .Include(e => e.Department)
                    .Where(e => e.EmploymentStatus == "Active")
                    .OrderBy(e => e.LastName)
                    .ThenBy(e => e.FirstName)
                    .ToListAsync();

                _allEmployees.Clear();
                foreach (var employee in list)
                {
                    _allEmployees.Add(employee);
                }

                FilterEmployees();
            }
            catch
            {
            }
        }

        private void FilterEmployees()
        {
            FilteredEmployees.Clear();
            var query = _allEmployees.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(EmployeeSearch))
            {
                var search = EmployeeSearch.Trim().ToLowerInvariant();
                query = query.Where(e =>
                    e.FullName.ToLowerInvariant().Contains(search) ||
                    e.EmployeeNo.ToLowerInvariant().Contains(search));
            }

            foreach (var employee in query)
            {
                FilteredEmployees.Add(employee);
            }
        }

        private async Task LoadPoliciesAsync()
        {
            EmployeePolicies.Clear();
            SelectedEmployeePolicy = null;
            OnPropertyChanged(nameof(HasPolicies));
            OnPropertyChanged(nameof(PolicyEmptyMessage));

            if (SelectedEmployee is null)
                return;

            IsLoadingPolicies = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var policies = await db.EmployeePolicies
                    .Include(ep => ep.Policy)
                    .Where(ep => ep.EmpId == SelectedEmployee.EmpId &&
                                 ep.AssignmentStatus == "Active")
                    .ToListAsync();

                foreach (var policy in policies)
                {
                    EmployeePolicies.Add(policy);
                }

                if (EmployeePolicies.Count == 1)
                {
                    SelectedEmployeePolicy = EmployeePolicies[0];
                }

                OnPropertyChanged(nameof(HasPolicies));
                OnPropertyChanged(nameof(PolicyEmptyMessage));
            }
            catch
            {
            }
            finally
            {
                IsLoadingPolicies = false;
            }
        }

        private bool Validate()
        {
            if (SelectedEmployee is null)
            {
                ErrorMessage = "Please select an employee.";
                return false;
            }

            if (SelectedEmployeePolicy is null)
            {
                ErrorMessage = "Please select a policy assignment.";
                return false;
            }

            if (MaxBenefit <= 0)
            {
                ErrorMessage = "Maximum benefit amount must be greater than zero.";
                return false;
            }

            ErrorMessage = string.Empty;
            return true;
        }

        private async Task SaveAsync()
        {
            if (!Validate())
                return;

            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                if (IsEditMode)
                {
                    var benefit = await db.Benefits.FindAsync(_editBenefitId);
                    if (benefit is null)
                    {
                        ErrorMessage = "Benefit record not found.";
                        return;
                    }

                    benefit.BenefitType = BenefitType;
                    benefit.YearPeriod = YearPeriod;
                    benefit.MaxBenefit = MaxBenefit;
                    benefit.Remaining = benefit.MaxBenefit - benefit.UsedBenefit;
                    benefit.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
                    benefit.UpdatedAt = DateTime.Now;

                    await db.SaveChangesAsync();
                    await AuditService.LogUpdate("benefits", benefit.BenefitId,
                        $"Benefit updated: {BenefitType} {YearPeriod} - PHP {MaxBenefit:N2}");
                }
                else
                {
                    var exists = await db.Benefits.AnyAsync(b =>
                        b.EpId == SelectedEmployeePolicy!.EpId &&
                        b.BenefitType == BenefitType &&
                        b.YearPeriod == YearPeriod);

                    if (exists)
                    {
                        ErrorMessage =
                            $"A {BenefitType} benefit for {YearPeriod} already exists for this assignment.";
                        return;
                    }

                    var benefit = new Benefit
                    {
                        EpId = SelectedEmployeePolicy!.EpId,
                        BenefitType = BenefitType,
                        YearPeriod = YearPeriod,
                        MaxBenefit = MaxBenefit,
                        UsedBenefit = 0,
                        Remaining = MaxBenefit,
                        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    db.Benefits.Add(benefit);
                    await db.SaveChangesAsync();
                    await AuditService.LogInsert("benefits", benefit.BenefitId,
                        $"Benefit added: {BenefitType} {YearPeriod} - PHP {MaxBenefit:N2}");
                }

                OnSaveSuccess?.Invoke();
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Save failed: {GetDetailedErrorMessage(ex)}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static decimal ParseMoney(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0m;

            var normalized = value.Replace(",", string.Empty).Trim();
            return decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var parsed)
                ? parsed
                : 0m;
        }

        private static string GetDetailedErrorMessage(Exception ex)
        {
            var messages = new List<string>();
            Exception? current = ex;

            while (current is not null)
            {
                if (!string.IsNullOrWhiteSpace(current.Message) &&
                    !messages.Contains(current.Message))
                {
                    messages.Add(current.Message);
                }

                current = current.InnerException;
            }

            return string.Join(" | ", messages);
        }
    }
}
