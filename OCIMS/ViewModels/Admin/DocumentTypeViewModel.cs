using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;

namespace eSureHi.ViewModels.Admin
{
    public class DocumentTypeViewModel : ObservableObject
    {
        // ── Collections ────────────────────────────────────────────────
        public ObservableCollection<DocumentType> DocumentTypes { get; } = new();

        // ── Selected ───────────────────────────────────────────────────
        private DocumentType? _selectedType;
        public DocumentType? SelectedType
        {
            get => _selectedType;
            set
            {
                SetProperty(ref _selectedType, value);
                if (value is not null)
                {
                    EditTypeName = value.TypeName;
                    EditDescription = value.Description ?? string.Empty;
                }
                OnPropertyChanged(nameof(HasSelection));
                SaveEditCommand.RaiseCanExecuteChanged();
                DeactivateCommand.RaiseCanExecuteChanged();
            }
        }
        public bool HasSelection => SelectedType is not null;

        // ── New Type Fields ────────────────────────────────────────────
        private string _newTypeName = string.Empty;
        private string _newDescription = string.Empty;

        public string NewTypeName
        {
            get => _newTypeName;
            set => SetProperty(ref _newTypeName, value);
        }
        public string NewDescription
        {
            get => _newDescription;
            set => SetProperty(ref _newDescription, value);
        }

        // ── Edit Fields ────────────────────────────────────────────────
        private string _editTypeName = string.Empty;
        private string _editDescription = string.Empty;

        public string EditTypeName
        {
            get => _editTypeName;
            set => SetProperty(ref _editTypeName, value);
        }
        public string EditDescription
        {
            get => _editDescription;
            set => SetProperty(ref _editDescription, value);
        }

        // ── State ──────────────────────────────────────────────────────
        private string _errorMessage = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _isBusy;

        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand AddCommand { get; }
        public RelayCommand SaveEditCommand { get; }
        public RelayCommand DeactivateCommand { get; }
        public RelayCommand CloseCommand { get; }

        public Action? CloseAction { get; set; }

        // ── Constructor ────────────────────────────────────────────────
        public DocumentTypeViewModel()
        {
            AddCommand = new RelayCommand(async () => await AddAsync());
            SaveEditCommand = new RelayCommand(async () => await SaveEditAsync(),
                                    () => SelectedType is not null);
            DeactivateCommand = new RelayCommand(async () => await DeactivateAsync(),
                                    () => SelectedType is not null);
            CloseCommand = new RelayCommand(() => CloseAction?.Invoke());

            _ = LoadAsync();
        }

        // ── Load ───────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var types = await db.DocumentTypes
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.TypeName)
                    .ToListAsync();

                DocumentTypes.Clear();
                foreach (var t in types) DocumentTypes.Add(t);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load failed: {ex.Message}";
            }
        }

        // ── Add ────────────────────────────────────────────────────────
        private async Task AddAsync()
        {
            if (string.IsNullOrWhiteSpace(NewTypeName))
            { ErrorMessage = "Type name is required."; return; }

            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                bool exists = await db.DocumentTypes
                    .AnyAsync(t => t.TypeName == NewTypeName.Trim());
                if (exists)
                { ErrorMessage = "A type with this name already exists."; return; }

                db.DocumentTypes.Add(new DocumentType
                {
                    TypeName = NewTypeName.Trim(),
                    Description = string.IsNullOrWhiteSpace(NewDescription)
                                      ? null : NewDescription.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.Now
                });
                await db.SaveChangesAsync();

                NewTypeName = string.Empty;
                NewDescription = string.Empty;
                StatusMessage = "Document type added.";
                ErrorMessage = string.Empty;
                await LoadAsync();
            }
            catch (Exception ex) { ErrorMessage = $"Failed: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        // ── Save Edit ──────────────────────────────────────────────────
        private async Task SaveEditAsync()
        {
            if (SelectedType is null) return;
            if (string.IsNullOrWhiteSpace(EditTypeName))
            { ErrorMessage = "Type name is required."; return; }

            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var t = await db.DocumentTypes.FindAsync(SelectedType.DocTypeId);
                if (t is null) return;

                t.TypeName = EditTypeName.Trim();
                t.Description = string.IsNullOrWhiteSpace(EditDescription)
                                    ? null : EditDescription.Trim();
                await db.SaveChangesAsync();

                StatusMessage = "Changes saved.";
                ErrorMessage = string.Empty;
                await LoadAsync();
            }
            catch (Exception ex) { ErrorMessage = $"Failed: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        // ── Deactivate ─────────────────────────────────────────────────
        private async Task DeactivateAsync()
        {
            if (SelectedType is null) return;

            var result = MessageBox.Show(
                $"Deactivate '{SelectedType.TypeName}'?\n" +
                "It will no longer appear in document type lists.",
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var t = await db.DocumentTypes.FindAsync(SelectedType.DocTypeId);
                if (t is null) return;
                t.IsActive = false;
                await db.SaveChangesAsync();
                StatusMessage = "Document type deactivated.";
                await LoadAsync();
            }
            catch (Exception ex) { ErrorMessage = $"Failed: {ex.Message}"; }
            finally { IsBusy = false; }
        }
    }
}
