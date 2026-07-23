using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;

namespace eSureHi.ViewModels.Admin
{
    public class TransactionDetailViewModel : ObservableObject
    {
        // ── Transaction ────────────────────────────────────────────────
        private DocumentTransaction? _transaction;
        public DocumentTransaction? Transaction
        {
            get => _transaction;
            set
            {
                SetProperty(ref _transaction, value);
                OnPropertyChanged(nameof(TxStatus));
                OnPropertyChanged(nameof(CanMarkInTransit));
                OnPropertyChanged(nameof(CanMarkReceived));
                OnPropertyChanged(nameof(CanMarkAcknowledged));
                OnPropertyChanged(nameof(CanComplete));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(PriorityColor));
            }
        }

        public string TxStatus => Transaction?.Status ?? string.Empty;
        public bool CanMarkInTransit => TxStatus == "Pending";
        public bool CanMarkReceived => TxStatus == "In Transit";
        public bool CanMarkAcknowledged => TxStatus == "Received";
        public bool CanComplete => TxStatus == "Acknowledged";
        public bool CanCancel => TxStatus is "Pending" or "In Transit";

        public string PriorityColor => Transaction?.Priority switch
        {
            "Urgent" => "#C62828",
            "High" => "#E65100",
            "Normal" => "#2E7D32",
            "Low" => "#757575",
            _ => "#2E7D32"
        };

        // ── Remarks for transition ─────────────────────────────────────
        private string _transitionRemarks = string.Empty;
        public string TransitionRemarks
        {
            get => _transitionRemarks;
            set => SetProperty(ref _transitionRemarks, value);
        }

        // ── State ──────────────────────────────────────────────────────
        private string _statusMessage = string.Empty;
        private bool _isBusy;
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand InTransitCommand { get; }
        public RelayCommand ReceivedCommand { get; }
        public RelayCommand AcknowledgedCommand { get; }
        public RelayCommand CompleteCommand { get; }
        public RelayCommand CancelTxCommand { get; }
        public RelayCommand CloseCommand { get; }

        public Action? CloseAction { get; set; }
        public Action? OnStatusChanged { get; set; }

        // ── Constructor ────────────────────────────────────────────────
        public TransactionDetailViewModel()
        {
            InTransitCommand = new RelayCommand(async () => await TransitionAsync("In Transit"));
            ReceivedCommand = new RelayCommand(async () => await TransitionAsync("Received"));
            AcknowledgedCommand = new RelayCommand(async () => await TransitionAsync("Acknowledged"));
            CompleteCommand = new RelayCommand(async () => await TransitionAsync("Completed"));
            CancelTxCommand = new RelayCommand(async () => await TransitionAsync("Cancelled"));
            CloseCommand = new RelayCommand(() => CloseAction?.Invoke());
        }

        // ── Load ───────────────────────────────────────────────────────
        public async Task LoadAsync(int txId)
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                Transaction = await db.DocumentTransactions
                    .Include(t => t.Sender)
                    .Include(t => t.Receiver)
                    .FirstOrDefaultAsync(t => t.TransactionId == txId);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load failed: {ex.Message}";
            }
        }

        // ── Transition ─────────────────────────────────────────────────
        private async Task TransitionAsync(string newStatus)
        {
            if (Transaction is null) return;

            string confirmMsg = newStatus switch
            {
                "In Transit" => "Mark this transaction as In Transit?",
                "Received" => "Mark this transaction as Received?",
                "Acknowledged" => "Mark this transaction as Acknowledged?",
                "Completed" => "Mark this transaction as Completed?",
                "Cancelled" => "Cancel this transaction? This cannot be undone.",
                _ => $"Change status to {newStatus}?"
            };

            var result = MessageBox.Show(confirmMsg, "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var tx = await db.DocumentTransactions.FindAsync(Transaction.TransactionId);
                if (tx is null) return;

                tx.Status = newStatus;
                tx.UpdatedAt = DateTime.Now;

                if (!string.IsNullOrWhiteSpace(TransitionRemarks))
                    tx.Remarks = TransitionRemarks.Trim();

                tx.ProcessedBy = AuthService.Instance.CurrentUser?.UserId;

                await db.SaveChangesAsync();

                // After TransitionAsync SaveChangesAsync:
                await AuditService.LogUpdate("document_transactions", tx.TransactionId,
                    $"Transaction {tx.TransactionNo} status changed to {newStatus}");

                await LoadAsync(Transaction.TransactionId);
                TransitionRemarks = string.Empty;
                OnStatusChanged?.Invoke();
            }
            catch (Exception ex) { StatusMessage = $"Failed: {ex.Message}"; }
            finally { IsBusy = false; }
        }
    }
}
