using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace eSureHi.ViewModels.Admin
{
    public class DocumentFormViewModel : ObservableObject
    {
        // ── Mode ───────────────────────────────────────────────────────
        public bool IsEditMode { get; private set; }
        public string DialogTitle => IsEditMode ? "Edit Document" : "Upload Document";
        private int _editDocId;

        // ── Employee Selection ─────────────────────────────────────────
        private ObservableCollection<Employee> _allEmployees = new();
        public ObservableCollection<Employee> FilteredEmployees { get; } = new();
        public ObservableCollection<DocumentType> DocumentTypes { get; } = new();

        private string _employeeSearch = string.Empty;
        public string EmployeeSearch
        {
            get => _employeeSearch;
            set { SetProperty(ref _employeeSearch, value); FilterEmployees(); }
        }

        private Employee? _selectedEmployee;
        public Employee? SelectedEmployee
        {
            get => _selectedEmployee;
            set
            {
                SetProperty(ref _selectedEmployee, value);
                OnPropertyChanged(nameof(HasEmployee));
            }
        }
        public bool HasEmployee => SelectedEmployee is not null;

        // ── Document Fields ────────────────────────────────────────────
        private DocumentType? _selectedDocType;
        private string _docTitle = string.Empty;
        private string _filePath = string.Empty;
        private string _fileName = string.Empty;
        private string _fileSize = string.Empty;
        private string _remarks = string.Empty;

        public DocumentType? SelectedDocType
        {
            get => _selectedDocType;
            set => SetProperty(ref _selectedDocType, value);
        }
        public string DocTitle
        {
            get => _docTitle;
            set => SetProperty(ref _docTitle, value);
        }
        public string FilePath
        {
            get => _filePath;
            set
            {
                SetProperty(ref _filePath, value);
                OnPropertyChanged(nameof(HasFile));
            }
        }
        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }
        public string FileSize
        {
            get => _fileSize;
            set => SetProperty(ref _fileSize, value);
        }
        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }
        public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

        // ── State ──────────────────────────────────────────────────────
        private string _errorMessage = string.Empty;
        private bool _isBusy;

        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
        public bool IsBusy { get => _isBusy; set { SetProperty(ref _isBusy, value); SaveCommand.RaiseCanExecuteChanged(); } }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand BrowseFileCommand { get; }
        public RelayCommand ClearFileCommand { get; }

        public Action? CloseAction { get; set; }
        public Action? OnSaveSuccess { get; set; }

        // ── Constructor ────────────────────────────────────────────────
        public DocumentFormViewModel()
        {
            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsBusy);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
            BrowseFileCommand = new RelayCommand(BrowseFile);
            ClearFileCommand = new RelayCommand(ClearFile);

            _ = LoadAsync();
        }

        // ── Init Edit ──────────────────────────────────────────────────
        public async Task InitEditAsync(int docId)
        {
            IsEditMode = true;
            _editDocId = docId;

            using var db = eSureHiDbContextFactory.Create();
            var d = await db.Documents
                .Include(x => x.Employee)
                .Include(x => x.DocumentType)
                .FirstOrDefaultAsync(x => x.DocumentId == docId);
            if (d is null) return;

            await LoadAsync();
            SelectedEmployee = _allEmployees
                .FirstOrDefault(e => e.EmpId == d.EmpId);
            SelectedDocType = DocumentTypes
                .FirstOrDefault(t => t.DocTypeId == d.DocTypeId);

            DocTitle = d.DocTitle;
            FilePath = d.FilePath ?? string.Empty;
            FileName = d.FileName ?? string.Empty;
            FileSize = d.FileSize ?? string.Empty;
            Remarks = d.Remarks ?? string.Empty;
        }

        // ── Load ───────────────────────────────────────────────────────
        private async Task LoadAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                var employees = await db.Employees
                    .Include(e => e.Department)
                    .Where(e => e.EmploymentStatus == "Active")
                    .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                    .ToListAsync();
                _allEmployees.Clear();
                foreach (var e in employees) _allEmployees.Add(e);
                FilterEmployees();

                var types = await db.DocumentTypes
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.TypeName)
                    .ToListAsync();
                DocumentTypes.Clear();
                foreach (var t in types) DocumentTypes.Add(t);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to load document types: {ex.Message}";
            }
        }

        private void FilterEmployees()
        {
            FilteredEmployees.Clear();
            var q = _allEmployees.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(EmployeeSearch))
            {
                var s = EmployeeSearch.Trim().ToLower();
                q = q.Where(e => e.FullName.ToLower().Contains(s) ||
                                 e.EmployeeNo.ToLower().Contains(s));
            }
            foreach (var e in q) FilteredEmployees.Add(e);
        }

        // ── Browse File ────────────────────────────────────────────────
        private void BrowseFile()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select Document File",
                Filter = "All Files|*.*|PDF|*.pdf|Images|*.jpg;*.jpeg;*.png|" +
                         "Word|*.doc;*.docx|Excel|*.xls;*.xlsx"
            };
            if (dlg.ShowDialog() != true) return;

            var info = new FileInfo(dlg.FileName);
            FilePath = dlg.FileName;
            FileName = info.Name;
            FileSize = FormatFileSize(info.Length);

            if (string.IsNullOrWhiteSpace(DocTitle))
                DocTitle = Path.GetFileNameWithoutExtension(info.Name);
        }

        private void ClearFile()
        {
            FilePath = string.Empty;
            FileName = string.Empty;
            FileSize = string.Empty;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:N1} MB";
            if (bytes >= 1_024) return $"{bytes / 1_024.0:N0} KB";
            return $"{bytes} B";
        }

        // ── Validate ───────────────────────────────────────────────────
        private bool Validate()
        {
            if (SelectedEmployee is null)
            { ErrorMessage = "Please select an employee."; return false; }
            if (!IsEditMode && !HasFile)
            { ErrorMessage = "Please choose a file to upload."; return false; }
            if (DocumentTypes.Count == 0)
            { ErrorMessage = "No document types found. Please seed them in Settings or add via Manage Document Types."; return false; }
            if (SelectedDocType is null)
            { ErrorMessage = "Please select a document type."; return false; }
            if (string.IsNullOrWhiteSpace(DocTitle))
            { ErrorMessage = "Document title is required."; return false; }
            ErrorMessage = string.Empty;
            return true;
        }

        // ── Save ───────────────────────────────────────────────────────
        private async Task SaveAsync()
        {
            if (!Validate()) return;
            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                if (IsEditMode)
                {
                    var d = await db.Documents.FindAsync(_editDocId);
                    if (d is null) { ErrorMessage = "Document not found."; return; }
                    MapToEntity(d);
                    d.UpdatedAt = DateTime.Now;
                    await db.SaveChangesAsync();

                    await AuditService.LogUpdate("documents", d.DocumentId,
                        $"Document info updated: {d.DocTitle} for {SelectedEmployee?.FullName}");
                }
                else
                {
                    var d = new Document
                    {
                        UploadedBy = AuthService.Instance.CurrentUser?.UserId,
                        IsActive = true,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    MapToEntity(d);
                    db.Documents.Add(d);
                    await db.SaveChangesAsync();

                    await AuditService.LogInsert("documents", d.DocumentId,
                        $"New document uploaded: {d.DocTitle} for {SelectedEmployee?.FullName}");
                }

                OnSaveSuccess?.Invoke();
                CloseAction?.Invoke();
            }
            catch (Exception ex) { ErrorMessage = $"Save failed: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        // ── Map to Entity ──────────────────────────────────────────────
        private void MapToEntity(Document d)
        {
            d.EmpId = SelectedEmployee!.EmpId;
            d.DocTypeId = SelectedDocType!.DocTypeId;
            d.DocTitle = DocTitle.Trim();
            d.FileName = string.IsNullOrWhiteSpace(FileName) ? string.Empty : FileName.Trim();
            d.FilePath = string.IsNullOrWhiteSpace(FilePath) ? string.Empty : FilePath.Trim();

            // ✅ Fixed CS8601 — use string.Empty instead of null
            // so non-nullable string properties are never assigned null
            d.FileSize = string.IsNullOrWhiteSpace(FileSize) ? string.Empty : FileSize.Trim();
            d.Remarks = string.IsNullOrWhiteSpace(Remarks) ? string.Empty : Remarks.Trim();
        }
    }
}
