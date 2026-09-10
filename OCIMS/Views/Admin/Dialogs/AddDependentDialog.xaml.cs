using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using eSureHi.Models;
using eSureHi.ViewModels.Admin;
using eSureHi.Data;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class AddDependentDialog : Window, INotifyPropertyChanged
    {
        private readonly BeneficiaryStaging _resident;
        private Employee? _selectedEmployee;
        private string _selectedMemberType = "New Member";
        private DateTime _dateRegistered = DateTime.Today;
        private string _relationship = "Other";
        private string _sourceOfFunds = "Job Order";
        private decimal _contribution;
        private string _cedulaNo = string.Empty;
        private bool _received;
        private string _displayFileName = "No file chosen";
        private string _selectedFilePath = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<Employee> AllEmployees { get; } = new();
        public ObservableCollection<Employee> FilteredEmployees { get; } = new();
        public ObservableCollection<string> MemberTypeOptions { get; } = new() { "New Member" };
        public ObservableCollection<string> RelationshipOptions { get; } = new() 
        { 
            "Spouse", "Son", "Daughter", "Child", "Parent", "Sibling", "Other" 
        };
        public ObservableCollection<string> SourceOfFundsOptions { get; } = new() 
        { 
            "Job Order", "Casual", "Regular", "Barangay Officials", "Senior Citizen" 
        };

        public string ResidentName => _resident.FullName ?? _resident.DisplayName ?? "Unknown";
        
        public string ResidentAge
        {
            get
            {
                if (DateOnly.TryParse(_resident.DateOfBirth, out var dob))
                {
                    var today = DateOnly.FromDateTime(DateTime.Today);
                    int age = today.Year - dob.Year;
                    if (dob > today.AddYears(-age)) age--;
                    return age.ToString();
                }
                return "Unknown";
            }
        }

        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                if (_selectedEmployee != value)
                {
                    _selectedEmployee = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedMemberType
        {
            get => _selectedMemberType;
            set
            {
                if (_selectedMemberType != value)
                {
                    _selectedMemberType = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime DateRegistered
        {
            get => _dateRegistered;
            set
            {
                if (_dateRegistered != value)
                {
                    _dateRegistered = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Relationship
        {
            get => _relationship;
            set
            {
                if (_relationship != value)
                {
                    _relationship = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SourceOfFunds
        {
            get => _sourceOfFunds;
            set
            {
                if (_sourceOfFunds != value)
                {
                    _sourceOfFunds = value;
                    OnPropertyChanged();
                    UpdateDefaultContribution();
                }
            }
        }

        public decimal Contribution
        {
            get => _contribution;
            set
            {
                if (_contribution != value)
                {
                    _contribution = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CedulaNo
        {
            get => _cedulaNo;
            set
            {
                if (_cedulaNo != value)
                {
                    _cedulaNo = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool Received
        {
            get => _received;
            set
            {
                if (_received != value)
                {
                    _received = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DisplayFileName
        {
            get => _displayFileName;
            set
            {
                if (_displayFileName != value)
                {
                    _displayFileName = value;
                    OnPropertyChanged();
                }
            }
        }

        public AddDependentDialog(BeneficiaryStaging resident)
        {
            InitializeComponent();
            _resident = resident;
            DataContext = this;

            // Autofill fields from staging record
            Relationship = MapRoleToRelationship(resident.DemographicFamilyRole);
            SourceOfFunds = "Job Order";
            CedulaNo = string.Empty;
            Received = false;
            UpdateDefaultContribution();

            _ = LoadEmployeesAsync();
        }

        private async Task LoadEmployeesAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var list = await db.Employees
                    .Where(e => e.EmploymentStatus == "Active")
                    .OrderBy(e => e.LastName)
                    .ThenBy(e => e.FirstName)
                    .ToListAsync();

                AllEmployees.Clear();
                foreach (var emp in list)
                {
                    AllEmployees.Add(emp);
                }

                FilterEmployees(string.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load primary members: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterEmployees(string searchText)
        {
            FilteredEmployees.Clear();
            var search = (searchText ?? string.Empty).Trim().ToLower();

            var query = AllEmployees.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e => e.FullName.ToLower().Contains(search) || 
                                         (e.EmployeeNo ?? string.Empty).ToLower().Contains(search));
            }

            foreach (var emp in query)
            {
                FilteredEmployees.Add(emp);
            }
        }

        private void EmployeeSearchCombo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is ComboBox cb)
            {
                FilterEmployees(cb.Text);
            }
        }

        private void ChooseFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Supporting Document",
                Filter = "All Files|*.*|PDF|*.pdf|Images|*.jpg;*.jpeg;*.png"
            };

            if (dlg.ShowDialog() == true)
            {
                _selectedFilePath = dlg.FileName;
                DisplayFileName = Path.GetFileName(dlg.FileName);
            }
        }

        private async void AddDependentButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEmployee is null)
            {
                MessageBox.Show("Please select a primary member.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var db = eSureHiDbContextFactory.Create();

                DateOnly? dob = null;
                if (DateOnly.TryParse(_resident.DateOfBirth, out var parsedDob))
                {
                    dob = parsedDob;
                }

                // Create the Beneficiary record linked to the selected Employee
                var beneficiary = new Beneficiary
                {
                    EmpId = SelectedEmployee.EmpId,
                    BeneficiaryId = _resident.BeneficiaryId ?? $"IMS-BEN-{Guid.NewGuid().ToString().Substring(0, 8)}",
                    CivilRegistryId = _resident.CivilRegistryId,
                    FirstName = _resident.FirstName ?? _resident.DisplayName ?? "N/A",
                    LastName = _resident.LastName ?? "N/A",
                    Relationship = Relationship,
                    DateOfBirth = dob,
                    Gender = _resident.Sex ?? "Other",
                    IsPrimary = false,
                    RecipientsInsurance = null,
                    CedulaNo = string.IsNullOrWhiteSpace(CedulaNo) ? null : CedulaNo.Trim(),
                    Received = Received,
                    Contribution = Contribution,
                    SourceOfFunds = string.IsNullOrWhiteSpace(SourceOfFunds) ? null : SourceOfFunds.Trim(),
                    WorkflowStatus = "Approved",
                    StatusRemarks = "Approved via Add Dependent to Primary Member",
                    IsActive = true,
                    IsAdminConfirmed = true,
                    CreatedAt = DateTime.Now
                };

                db.Beneficiaries.Add(beneficiary);
                await db.SaveChangesAsync();

                // Save Uploaded Document if chosen
                if (!string.IsNullOrWhiteSpace(_selectedFilePath) && File.Exists(_selectedFilePath))
                {
                    var fileInfo = new FileInfo(_selectedFilePath);
                    var document = new Document
                    {
                        EmpId = SelectedEmployee.EmpId,
                        DocTypeId = 1, // Default or generic supporting document type
                        DocTitle = $"Dependent Document - {beneficiary.FullName}",
                        FileName = fileInfo.Name,
                        FilePath = _selectedFilePath,
                        FileSize = $"{fileInfo.Length / 1024} KB",
                        Remarks = $"Uploaded during dependent registration of {beneficiary.FullName}",
                        IsActive = true,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    db.Documents.Add(document);
                    await db.SaveChangesAsync();
                }

                // Link the staging record
                var staging = await db.BeneficiaryStaging.FindAsync(_resident.StagingId);
                if (staging != null)
                {
                    staging.LinkStatus = "Linked";
                    staging.LinkedEmpId = SelectedEmployee.EmpId;
                    staging.LinkedBenId = beneficiary.BenId;
                    await db.SaveChangesAsync();
                }

                MessageBox.Show("Dependent successfully added and linked to the primary member.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save dependent: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateDefaultContribution()
        {
            Contribution = SourceOfFunds switch
            {
                "Regular" => 200.00m,
                "Casual" => 150.00m,
                "Job Order" => 100.00m,
                "Barangay Officials" => 50.00m,
                "Senior Citizen" => 0.00m,
                _ => 100.00m
            };
        }

        private static string MapRoleToRelationship(string? role)
        {
            if (string.IsNullOrWhiteSpace(role)) return "Other";
            return role.ToUpperInvariant() switch
            {
                "SPOUSE" => "Spouse",
                "SON" => "Son",
                "DAUGHTER" => "Daughter",
                "CHILD" or "CHILDREN" => "Child",
                "FATHER" or "MOTHER" or "PARENT" => "Parent",
                "BROTHER" or "SISTER" or "SIBLING" => "Sibling",
                _ => "Other"
            };
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
