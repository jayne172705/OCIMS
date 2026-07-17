using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
    public class DocumentsViewModel : ObservableObject
    {
        // ── Collections ────────────────────────────────────────────────
        private ObservableCollection<Document> _allDocuments = new();
        public ObservableCollection<Document> DisplayedDocuments { get; } = new();
        public ObservableCollection<DocumentType> DocumentTypes { get; } = new();

        // ── Selected ───────────────────────────────────────────────────
        private Document? _selectedDocument;
        public Document? SelectedDocument
        {
            get => _selectedDocument;
            set
            {
                SetProperty(ref _selectedDocument, value);
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(CanOpenFile));
                EditCommand.RaiseCanExecuteChanged();
                OpenFileCommand.RaiseCanExecuteChanged();
                DeactivateCommand.RaiseCanExecuteChanged();
            }
        }
        public bool HasSelection => SelectedDocument is not null;
        public bool CanOpenFile => SelectedDocument is not null &&
                                    !string.IsNullOrWhiteSpace(SelectedDocument.FilePath) &&
                                    File.Exists(SelectedDocument.FilePath);

        // ── Filters ────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        private DocumentType? _typeFilter;

        public string SearchText
        {
            get => _searchText;
            set { SetProperty(ref _searchText, value); ApplyFilter(); }
        }
        public DocumentType? TypeFilter
        {
            get => _typeFilter;
            set { SetProperty(ref _typeFilter, value); ApplyFilter(); }
        }

        // ── Counts ─────────────────────────────────────────────────────
        private int _totalCount;
        private int _filteredCount;
        public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
        public int FilteredCount { get => _filteredCount; set => SetProperty(ref _filteredCount, value); }

        // ── State ──────────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand UploadCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand OpenFileCommand { get; }
        public RelayCommand DeactivateCommand { get; }
        public RelayCommand ManageTypesCommand { get; }
        public RelayCommand ClearFilterCommand { get; }
        public RelayCommand BackToDashboardCommand { get; }

        // ── Constructor ────────────────────────────────────────────────
        public DocumentsViewModel()
        {
            UploadCommand = new RelayCommand(OpenUploadDialog);
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            EditCommand = new RelayCommand(OpenEditDialog,
                                     () => SelectedDocument is not null);
            OpenFileCommand = new RelayCommand(OpenFile,
                                     () => CanOpenFile);
            DeactivateCommand = new RelayCommand(async () => await DeactivateAsync(),
                                     () => SelectedDocument is not null);
            ManageTypesCommand = new RelayCommand(OpenManageTypesDialog);
            ClearFilterCommand = new RelayCommand(ClearFilters);
            BackToDashboardCommand = new RelayCommand(NavigateToDashboard);

            _ = LoadAsync();
        }

        private void NavigateToDashboard()
        {
            NavigationService.Instance.NavigateTo(new Views.Admin.UserControls.HomeView());
        }

        // ── Load ───────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                var docs = await db.Documents
                    .Include(d => d.Employee)
                    .Include(d => d.DocumentType)
                    .Where(d => d.IsActive)
                    .OrderByDescending(d => d.CreatedAt)
                    .ToListAsync();

                var types = await db.DocumentTypes
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.TypeName)
                    .ToListAsync();

                _allDocuments.Clear();
                foreach (var d in docs) _allDocuments.Add(d);

                DocumentTypes.Clear();
                foreach (var t in types) DocumentTypes.Add(t);

                TotalCount = _allDocuments.Count;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load documents failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsLoading = false; }
        }

        // ── Filter ─────────────────────────────────────────────────────
        private void ApplyFilter()
        {
            var q = _allDocuments.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var s = SearchText.Trim().ToLower();
                q = q.Where(d =>
                    d.DocTitle.ToLower().Contains(s) ||
                    (d.Employee?.FullName.ToLower().Contains(s) ?? false) ||
                    (d.FileName?.ToLower().Contains(s) ?? false));
            }
            if (TypeFilter is not null)
                q = q.Where(d => d.DocTypeId == TypeFilter.DocTypeId);

            DisplayedDocuments.Clear();
            foreach (var d in q) DisplayedDocuments.Add(d);
            FilteredCount = DisplayedDocuments.Count;
        }

        // ── Upload ─────────────────────────────────────────────────────
        private void OpenUploadDialog()
        {
            var dialog = new Views.Admin.Dialogs.DocumentFormDialog();
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        // ── Edit ───────────────────────────────────────────────────────
        private void OpenEditDialog()
        {
            if (SelectedDocument is null) return;
            var dialog = new Views.Admin.Dialogs.DocumentFormDialog(
                SelectedDocument.DocumentId);
            dialog.SetSaveCallback(async () => await LoadAsync());
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
        }

        // ── Open File ──────────────────────────────────────────────────
        private void OpenFile()
        {
            if (SelectedDocument?.FilePath is null) return;
            if (!File.Exists(SelectedDocument.FilePath))
            {
                MessageBox.Show(
                    "File not found. It may have been moved or deleted.\n\n" +
                    $"Path: {SelectedDocument.FilePath}",
                    "File Not Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = SelectedDocument.FilePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open file: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Deactivate ─────────────────────────────────────────────────
        private async Task DeactivateAsync()
        {
            if (SelectedDocument is null) return;

            var result = MessageBox.Show(
                $"Remove '{SelectedDocument.DocTitle}' from the list?\n\n" +
                "The actual file will not be deleted.",
                "Confirm Remove",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var d = await db.Documents.FindAsync(SelectedDocument.DocumentId);
                if (d is null) return;
                d.IsActive = false;
                d.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Manage Types ───────────────────────────────────────────────
        private void OpenManageTypesDialog()
        {
            var dialog = new Views.Admin.Dialogs.ManageDocumentTypesDialog();
            if (App.ActiveShell != null && App.ActiveShell != dialog) dialog.Owner = App.ActiveShell;
            dialog.ShowDialog();
            _ = LoadAsync(); // refresh types after managing
        }

        // ── Clear Filters ──────────────────────────────────────────────
        private void ClearFilters()
        {
            _searchText = string.Empty;
            _typeFilter = null;
            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(TypeFilter));
            ApplyFilter();
        }
    }
}
