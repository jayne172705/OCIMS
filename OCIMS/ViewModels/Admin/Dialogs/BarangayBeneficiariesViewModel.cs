using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Models;
using eSureHi.Helpers;

namespace eSureHi.ViewModels.Admin.Dialogs
{
    public class RegisteredBeneficiaryRow
    {
        public Beneficiary Beneficiary { get; }
        public string FullName { get; }
        public string Relationship { get; }
        public string FamilyHead { get; }
        public string Program { get; }
        public string Status { get; }

        public RegisteredBeneficiaryRow(Beneficiary b)
        {
            Beneficiary = b;
            FullName = b.FullName;
            Relationship = b.IsPrimary ? "Head of Family" : (string.IsNullOrWhiteSpace(b.Relationship) ? "Dependent" : b.Relationship);
            FamilyHead = b.Employee?.FullName ?? "N/A";
            Program = b.SourceOfFunds ?? b.Employee?.EmploymentType ?? "N/A";
            Status = b.WorkflowStatus;
        }
    }

    public class BarangayBeneficiariesViewModel : ObservableObject
    {
        public string BarangayDisplay { get; }
        public ObservableCollection<RegisteredBeneficiaryRow> Beneficiaries { get; } = new();

        public string Barangay => _barangay;
        public ICommand OpenProfileCommand { get; }
        public Action<Employee>? OpenProfileRequested { get; set; }

        private readonly string _barangay;
        private readonly System.Collections.Generic.List<RegisteredBeneficiaryRow> _allBeneficiaries = new();
        private string _searchQuery = string.Empty;

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                ApplyFilter();
            }
        }

        public BarangayBeneficiariesViewModel(string barangay)
        {
            _barangay = barangay;
            BarangayDisplay = $"Barangay {barangay}";

            OpenProfileCommand = new RelayCommand<RegisteredBeneficiaryRow>(row =>
            {
                if (row?.Beneficiary?.Employee != null)
                {
                    OpenProfileRequested?.Invoke(row.Beneficiary.Employee);
                }
                else if (row is not null)
                {
                    System.Windows.MessageBox.Show(
                        $"{row.FullName} does not have an associated employee record to open profile.",
                        "No Profile Available",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            });
        }

        public void ApplyFilter()
        {
            var query = _searchQuery?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                Beneficiaries.Clear();
                foreach (var item in _allBeneficiaries)
                {
                    Beneficiaries.Add(item);
                }
            }
            else
            {
                var filtered = _allBeneficiaries
                    .Where(b => b.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               b.Relationship.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               b.FamilyHead.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               b.Program.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               b.Status.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                Beneficiaries.Clear();
                foreach (var item in filtered)
                {
                    Beneficiaries.Add(item);
                }
            }
        }

        public async Task LoadAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                
                var addressPattern = $"%{_barangay}%";

                // Get all active beneficiaries where either their employee's barangay matches
                // OR they have a CRS record with address containing the barangay.
                var crsBenIds = await db.CrsBeneficiaryCache
                    .Where(c => c.Address != null && EF.Functions.Like(c.Address, addressPattern))
                    .Select(c => c.BeneficiaryId)
                    .ToListAsync();
                var crsCivilIds = await db.CrsBeneficiaryCache
                    .Where(c => c.Address != null && EF.Functions.Like(c.Address, addressPattern))
                    .Select(c => c.CivilRegistryId)
                    .ToListAsync();

                var list = await db.Beneficiaries
                    .Include(b => b.Employee)
                    .Where(b => b.IsActive && (
                        (b.Employee != null && (b.Employee.Barangay == _barangay || EF.Functions.Like(b.Employee.Barangay, addressPattern))) ||
                        (b.BeneficiaryId != null && crsBenIds.Contains(b.BeneficiaryId)) ||
                        (b.CivilRegistryId != null && crsCivilIds.Contains(b.CivilRegistryId)) ||
                        (b.Employee != null && (crsBenIds.Contains(b.Employee.EmployeeNo) || crsCivilIds.Contains(b.Employee.EmployeeNo)))
                    ))
                    .OrderBy(b => b.LastName)
                    .ThenBy(b => b.FirstName)
                    .ToListAsync();

                _allBeneficiaries.Clear();
                foreach (var b in list)
                {
                    _allBeneficiaries.Add(new RegisteredBeneficiaryRow(b));
                }

                ApplyFilter();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error loading Barangay beneficiaries: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
