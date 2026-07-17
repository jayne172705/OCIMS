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
    public class TransactionFormViewModel : ObservableObject
    {
        // ── Mode ───────────────────────────────────────────────────────
        public bool IsEditMode { get; private set; }
        public string DialogTitle => IsEditMode ? "Edit Transaction" : "New Transaction";
        private int _editTxId;

        // ── Collections ────────────────────────────────────────────────
        public ObservableCollection<Sender> Senders { get; } = new();
        public ObservableCollection<Receiver> Receivers { get; } = new();
        public ObservableCollection<Document> Documents { get; } = new();

        // ── Fields ─────────────────────────────────────────────────────
        private string _transactionType = "Incoming";
        private Sender? _selectedSender;
        private Receiver? _selectedReceiver;
        private Document? _selectedDocument;
        private string _subject = string.Empty;
        private string _description = string.Empty;
        private DateTime? _transactionDate = DateTime.Today;
        private DateTime? _dueDate;
        private string _priority = "Normal";
        private string _remarks = string.Empty;

        public string TransactionType
        {
            get => _transactionType;
            set => SetProperty(ref _transactionType, value);
        }
        public Sender? SelectedSender
        {
            get => _selectedSender;
            set => SetProperty(ref _selectedSender, value);
        }
        public Receiver? SelectedReceiver
        {
            get => _selectedReceiver;
            set => SetProperty(ref _selectedReceiver, value);
        }
        public Document? SelectedDocument
        {
            get => _selectedDocument;
            set => SetProperty(ref _selectedDocument, value);
        }
        public string Subject
        {
            get => _subject;
            set => SetProperty(ref _subject, value);
        }
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
        public DateTime? TransactionDate
        {
            get => _transactionDate;
            set => SetProperty(ref _transactionDate, value);
        }
        public DateTime? DueDate
        {
            get => _dueDate;
            set => SetProperty(ref _dueDate, value);
        }
        public string Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }
        public string Remarks
        {
            get => _remarks;
            set => SetProperty(ref _remarks, value);
        }

        // ── Quick-Add Sender ───────────────────────────────────────────
        private bool _showAddSender;
        private string _newSenderName = string.Empty;
        private string _newSenderType = "External";

        public bool ShowAddSender
        {
            get => _showAddSender;
            set => SetProperty(ref _showAddSender, value);
        }
        public string NewSenderName
        {
            get => _newSenderName;
            set => SetProperty(ref _newSenderName, value);
        }
        public string NewSenderType
        {
            get => _newSenderType;
            set => SetProperty(ref _newSenderType, value);
        }

        // ── Quick-Add Receiver ─────────────────────────────────────────
        private bool _showAddReceiver;
        private string _newReceiverName = string.Empty;
        private string _newReceiverType = "External";

        public bool ShowAddReceiver
        {
            get => _showAddReceiver;
            set => SetProperty(ref _showAddReceiver, value);
        }
        public string NewReceiverName
        {
            get => _newReceiverName;
            set => SetProperty(ref _newReceiverName, value);
        }
        public string NewReceiverType
        {
            get => _newReceiverType;
            set => SetProperty(ref _newReceiverType, value);
        }

        // ── Static Lists ───────────────────────────────────────────────
        public string[] TransactionTypes { get; } =
            { "Incoming", "Outgoing", "Internal Transfer" };
        public string[] PriorityOptions { get; } =
            { "Low", "Normal", "High", "Urgent" };
        public string[] SenderTypes { get; } =
            { "Employee", "Client", "External", "Government", "Organization" };
        public string[] ReceiverTypes { get; } =
            { "Employee", "Client", "Department", "External", "Government", "Organization" };

        // ── State ──────────────────────────────────────────────────────
        private string _errorMessage = string.Empty;
        private bool _isBusy;

        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
        public bool IsBusy { get => _isBusy; set { SetProperty(ref _isBusy, value); SaveCommand.RaiseCanExecuteChanged(); } }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand ToggleAddSenderCommand { get; }
        public RelayCommand AddSenderCommand { get; }
        public RelayCommand ToggleAddReceiverCommand { get; }
        public RelayCommand AddReceiverCommand { get; }
        public RelayCommand ClearDocumentCommand { get; }

        public Action? CloseAction { get; set; }
        public Action? OnSaveSuccess { get; set; }

        // ── Constructor ────────────────────────────────────────────────
        public TransactionFormViewModel()
        {
            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsBusy);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
            ToggleAddSenderCommand = new RelayCommand(() => ShowAddSender = !ShowAddSender);
            ToggleAddReceiverCommand = new RelayCommand(() => ShowAddReceiver = !ShowAddReceiver);
            AddSenderCommand = new RelayCommand(async () => await AddSenderAsync());
            AddReceiverCommand = new RelayCommand(async () => await AddReceiverAsync());
            ClearDocumentCommand = new RelayCommand(() => SelectedDocument = null);

            _ = LoadAsync();
        }

        // ── Init Edit ──────────────────────────────────────────────────
        public async Task InitEditAsync(int txId)
        {
            IsEditMode = true;
            _editTxId = txId;

            await LoadAsync();

            using var db = eSureHiDbContextFactory.Create();
            var tx = await db.DocumentTransactions.FindAsync(txId);
            if (tx is null) return;

            TransactionType = tx.TransactionType;
            Subject = tx.Subject;
            Description = tx.Description ?? string.Empty;
            Priority = tx.Priority;
            Remarks = tx.Remarks ?? string.Empty;
            TransactionDate = tx.TransactionDate.HasValue
                                   ? tx.TransactionDate.Value.ToDateTime(TimeOnly.MinValue) : null;
            DueDate = tx.DueDate.HasValue
                                   ? tx.DueDate.Value.ToDateTime(TimeOnly.MinValue) : null;
            SelectedSender = Senders.FirstOrDefault(s => s.SenderId == tx.SenderId);
            SelectedReceiver = Receivers.FirstOrDefault(r => r.ReceiverId == tx.ReceiverId);
            SelectedDocument = Documents.FirstOrDefault(d => d.DocumentId == tx.DocumentId);
        }

        // ── Load ───────────────────────────────────────────────────────
        private async Task LoadAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                var senders = await db.Senders
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.SenderName)
                    .ToListAsync();
                Senders.Clear();
                foreach (var s in senders) Senders.Add(s);

                var receivers = await db.Receivers
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.ReceiverName)
                    .ToListAsync();
                Receivers.Clear();
                foreach (var r in receivers) Receivers.Add(r);

                var docs = await db.Documents
                    .Include(d => d.Employee)
                    .Where(d => d.IsActive)
                    .OrderByDescending(d => d.CreatedAt)
                    .Take(50)
                    .ToListAsync();
                Documents.Clear();
                foreach (var d in docs) Documents.Add(d);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Load transaction data failed: {ex.Message}";
            }
        }

        // ── Quick-Add Sender ───────────────────────────────────────────
        private async Task AddSenderAsync()
        {
            if (string.IsNullOrWhiteSpace(NewSenderName))
            { ErrorMessage = "Sender name is required."; return; }

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var s = new Sender
                {
                    SenderName = NewSenderName.Trim(),
                    SenderType = NewSenderType,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                db.Senders.Add(s);
                await db.SaveChangesAsync();

                await LoadAsync();
                SelectedSender = Senders.FirstOrDefault(x => x.SenderId == s.SenderId);
                NewSenderName = string.Empty;
                ShowAddSender = false;
                ErrorMessage = string.Empty;
            }
            catch (Exception ex) { ErrorMessage = $"Failed: {ex.Message}"; }
        }

        // ── Quick-Add Receiver ─────────────────────────────────────────
        private async Task AddReceiverAsync()
        {
            if (string.IsNullOrWhiteSpace(NewReceiverName))
            { ErrorMessage = "Receiver name is required."; return; }

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var r = new Receiver
                {
                    ReceiverName = NewReceiverName.Trim(),
                    ReceiverType = NewReceiverType,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                db.Receivers.Add(r);
                await db.SaveChangesAsync();

                await LoadAsync();
                SelectedReceiver = Receivers.FirstOrDefault(x => x.ReceiverId == r.ReceiverId);
                NewReceiverName = string.Empty;
                ShowAddReceiver = false;
                ErrorMessage = string.Empty;
            }
            catch (Exception ex) { ErrorMessage = $"Failed: {ex.Message}"; }
        }

        // ── Validate ───────────────────────────────────────────────────
        private bool Validate()
        {
            if (string.IsNullOrWhiteSpace(Subject))
            { ErrorMessage = "Subject is required."; return false; }
            if (TransactionDate is null)
            { ErrorMessage = "Transaction date is required."; return false; }
            if (DueDate.HasValue && TransactionDate.HasValue &&
                DueDate.Value.Date < TransactionDate.Value.Date)
            { ErrorMessage = "Due date cannot be earlier than the transaction date."; return false; }
            if (SelectedSender is null)
            { ErrorMessage = "⚠ Sender is required. Go to the Sender & Receiver tab."; return false; }
            if (SelectedReceiver is null)
            { ErrorMessage = "⚠ Receiver is required. Go to the Sender & Receiver tab."; return false; }
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
                    var tx = await db.DocumentTransactions.FindAsync(_editTxId);
                    if (tx is null) { ErrorMessage = "Transaction not found."; return; }
                    MapToEntity(tx);
                    tx.UpdatedAt = DateTime.Now;
                    await db.SaveChangesAsync();

                    // After edit:
                    await AuditService.LogUpdate("document_transactions", _editTxId,
                        $"Transaction updated: {tx.TransactionNo}");

                }
                else
                {
                    var tx = new DocumentTransaction
                    {
                        TransactionNo = GenerateTxNo(),
                        Status = "Pending",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    MapToEntity(tx);
                    db.DocumentTransactions.Add(tx);
                    await db.SaveChangesAsync();

                    // After new transaction:
                    await AuditService.LogInsert("document_transactions", tx.TransactionId,
                        $"Transaction created: {tx.TransactionNo} — {tx.Subject}");

                }

                OnSaveSuccess?.Invoke();
                CloseAction?.Invoke();
            }
            catch (Exception ex) { ErrorMessage = $"Save failed: {ex.Message}"; }
            finally { IsBusy = false; }
        }

        private void MapToEntity(DocumentTransaction tx)
        {
            tx.TransactionType = TransactionType;
            tx.SenderId = SelectedSender?.SenderId;
            tx.ReceiverId = SelectedReceiver?.ReceiverId;
            tx.DocumentId = SelectedDocument?.DocumentId;
            tx.Subject = Subject.Trim();
            tx.Description = string.IsNullOrWhiteSpace(Description)
                                      ? null : Description.Trim();
            tx.TransactionDate = TransactionDate.HasValue
                                      ? DateOnly.FromDateTime(TransactionDate.Value) : null;
            tx.DueDate = DueDate.HasValue
                                      ? DateOnly.FromDateTime(DueDate.Value) : null;
            tx.Priority = Priority;
            tx.Remarks = string.IsNullOrWhiteSpace(Remarks)
                                      ? null : Remarks.Trim();
            tx.ProcessedBy = AuthService.Instance.CurrentUser?.UserId;
        }

        private static string GenerateTxNo()
            => $"TXN-{DateTime.Now:yyMMddHHmmss}";
    }
}
