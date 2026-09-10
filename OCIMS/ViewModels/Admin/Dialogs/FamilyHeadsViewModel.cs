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
    public class BarangayBeneficiaryDisplay
    {
        public Beneficiary? Beneficiary { get; }
        public string FullName { get; }
        public string RelationshipText { get; }
        public string FamilyHeadName { get; }
        public string Status { get; }
        public CrsBeneficiaryCache? CrsRecord { get; }

        public BarangayBeneficiaryDisplay(Beneficiary b)
        {
            Beneficiary = b;
            FullName = b.FullName;
            RelationshipText = b.IsPrimary ? "Head of Family" : (string.IsNullOrWhiteSpace(b.Relationship) ? "Dependent" : b.Relationship);
            FamilyHeadName = b.Employee?.FullName ?? "N/A";
            Status = b.IsActive ? b.WorkflowStatus : "Inactive";
            CrsRecord = null;
        }

        public BarangayBeneficiaryDisplay(string fullName, string relationship, string familyHead, string status, Beneficiary? b = null, CrsBeneficiaryCache? crs = null)
        {
            Beneficiary = b;
            FullName = fullName;
            RelationshipText = relationship;
            FamilyHeadName = familyHead;
            Status = status;
            CrsRecord = crs;
        }
    }

    public class FamilyHeadsViewModel
    {
        public string BarangayDisplay { get; }
        public ObservableCollection<BarangayBeneficiaryDisplay> Beneficiaries { get; } = new();

        public string Barangay => _barangay;
        public ICommand OpenProfileCommand { get; }
        public Action<Employee>? OpenProfileRequested { get; set; }
        public ICommand AddBeneficiaryCommand { get; }
        public Action? AddBeneficiaryRequested { get; set; }
        public ICommand RegisterResidentCommand { get; }
        public Func<BarangayBeneficiaryDisplay, Task>? RegisterResidentRequested { get; set; }

        private readonly string _barangay;
        private readonly System.Collections.Generic.List<BarangayBeneficiaryDisplay> _allBeneficiaries = new();
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

        public FamilyHeadsViewModel(string barangay)
        {
            _barangay = barangay;
            BarangayDisplay = $"Barangay {barangay}";
            
            OpenProfileCommand = new RelayCommand<BarangayBeneficiaryDisplay>(display => 
            {
                if (display?.Beneficiary?.Employee != null)
                {
                    OpenProfileRequested?.Invoke(display.Beneficiary.Employee);
                }
                else if (display is not null)
                {
                    System.Windows.MessageBox.Show(
                        $"{display.FullName} is not yet registered in the insurance system.\n\nUse the 'Register' button to register them.",
                        "Unregistered Resident",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            });

            AddBeneficiaryCommand = new RelayCommand(() => AddBeneficiaryRequested?.Invoke());

            RegisterResidentCommand = new RelayCommand<BarangayBeneficiaryDisplay>(async display =>
            {
                if (display is not null && RegisterResidentRequested is not null)
                {
                    await RegisterResidentRequested(display);
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
                               b.RelationshipText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               b.FamilyHeadName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
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
                
                var totalCount = await db.CrsBeneficiaryCache.CountAsync();

                var crsRecords = await db.CrsBeneficiaryCache
                    .Where(c => c.Address != null && EF.Functions.Like(c.Address, addressPattern))
                    .OrderBy(c => c.LastName)
                    .ThenBy(c => c.FirstName)
                    .ToListAsync();

                var activeBens = await db.Beneficiaries
                    .Include(b => b.Employee)
                    .Where(b => b.IsActive)
                    .ToListAsync();

                var familyIds = crsRecords
                    .Select(c => c.FamilyId)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Distinct()
                    .ToList();

                var headsList = await db.CrsBeneficiaryCache
                    .Where(c => familyIds.Contains(c.FamilyId) && c.IsHouseholdHead && c.FamilyId != null)
                    .Select(c => new { c.FamilyId, FullName = c.FullName ?? (c.FirstName + " " + c.LastName).Trim() })
                    .ToListAsync();

                var heads = headsList
                    .GroupBy(c => c.FamilyId!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().FullName, StringComparer.OrdinalIgnoreCase);

                _allBeneficiaries.Clear();
                foreach (var crs in crsRecords)
                {
                    var matchedBen = activeBens.FirstOrDefault(b =>
                        (!string.IsNullOrEmpty(crs.BeneficiaryId) && (b.BeneficiaryId == crs.BeneficiaryId || b.CivilRegistryId == crs.BeneficiaryId || (b.Employee != null && b.Employee.EmployeeNo == crs.BeneficiaryId))) ||
                        (!string.IsNullOrEmpty(crs.CivilRegistryId) && (b.CivilRegistryId == crs.CivilRegistryId || b.BeneficiaryId == crs.CivilRegistryId || (b.Employee != null && b.Employee.EmployeeNo == crs.CivilRegistryId))) ||
                        (!string.IsNullOrEmpty(crs.ResidentsId?.ToString()) && (b.BeneficiaryId == crs.ResidentsId.ToString() || (b.Employee != null && b.Employee.EmployeeNo == crs.ResidentsId.ToString()))) ||
                        (string.Equals(b.FirstName, crs.FirstName, StringComparison.OrdinalIgnoreCase) && 
                         string.Equals(b.LastName, crs.LastName, StringComparison.OrdinalIgnoreCase)));

                    // If already registered in the system, skip so they disappear from the unregistered List
                    if (matchedBen != null)
                        continue;

                    string relationship = crs.RelationshipToHead ?? crs.FamilyRole ?? (crs.IsHouseholdHead ? "Head of Family" : "Dependent");
                    if (crs.IsHouseholdHead && string.IsNullOrEmpty(relationship))
                        relationship = "Head of Family";
                    else if (string.IsNullOrEmpty(relationship))
                        relationship = "Dependent";

                    string familyHead = "N/A";
                    if (!string.IsNullOrEmpty(crs.FamilyId) && heads.TryGetValue(crs.FamilyId, out var headName))
                    {
                        familyHead = headName;
                    }

                    _allBeneficiaries.Add(new BarangayBeneficiaryDisplay(
                        crs.FullName ?? $"{crs.FirstName} {crs.LastName}".Trim(),
                        relationship,
                        familyHead,
                        "Unregistered",
                        null,
                        crs
                    ));
                }

                ApplyFilter();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error loading Barangay residents: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
