using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using eSureHi.Data;
using eSureHi.Models;
using eSureHi.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MaterialDesignThemes.Wpf;
using eSureHi.Views.Admin.Dialogs;

namespace eSureHi.ViewModels.Admin
{
    public class DistributionRecordViewModel : ObservableObject
    {
        private DistributionRecord _model;
        public DistributionRecord Model => _model;

        public DistributionRecordViewModel(DistributionRecord model)
        {
            _model = model;
        }

        public string BeneficiaryName => _model.Beneficiary?.FullName ?? "Unknown";
        public string BeneficiaryId => _model.Beneficiary?.BeneficiaryId ?? _model.BeneficiaryId.ToString();
        public DateTime? ProcessedAt => _model.ProcessedAt;

        public string Status
        {
            get => _model.Status;
            set
            {
                if (SetProperty(_model.Status, value, _model, (m, v) => m.Status = v))
                {
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public string? Remarks
        {
            get => _model.Remarks;
            set
            {
                if (SetProperty(_model.Remarks, value, _model, (m, v) => m.Remarks = v))
                {
                    OnPropertyChanged(nameof(Remarks));
                }
            }
        }
    }

    /// <summary>
    /// Registrar works an unsaved queue of people still to process; Admin reviews
    /// already-persisted outcomes. The two modes read from different sources.
    /// </summary>
    public enum DistributionBoardMode
    {
        Registrar,
        AdminHistory
    }

    public class DistributionBatchViewModel : ObservableObject
    {
        private readonly eSureHiDbContext _dbContext;

        public DistributionBoardMode Mode { get; }
        public bool IsRegistrarMode => Mode == DistributionBoardMode.Registrar;
        public bool IsAdminHistoryMode => Mode == DistributionBoardMode.AdminHistory;

        public bool ShowReleasedColumn => IsAdminHistoryMode;
        public bool ShowUnreleasedColumn => IsRegistrarMode;

        // Both boards surface Rejected: the registrar needs to see Admin's decision come
        // back, and Admin needs rejected records to stay visible after they leave Pending.
        public bool ShowRejectedColumn => true;
        public bool ShowBatchActions => IsRegistrarMode;
        public bool ShowDragDropHint => IsRegistrarMode;

        // Searching and scanning drive the registrar's queue. Admin reviews a persisted
        // snapshot and never scans, so the whole search/scan cluster is registrar-only.
        public bool ShowSearchAndScan => IsRegistrarMode;

        /// <summary>
        /// Per-row Approve/Reject buttons on the Pending column. Admin-side only, and
        /// gated on the same review permission that governs claims and member approvals.
        /// Deliberately separate from <see cref="ShowBatchActions"/> so this never
        /// reintroduces Confirm/Rename/Reuse/Delete for Admin.
        /// </summary>
        public bool ShowAdminRowActions => IsAdminHistoryMode && PermissionService.CanApproveWorkflow;

        private decimal _totalFunds;
        public decimal TotalFunds { get => _totalFunds; set => SetProperty(ref _totalFunds, value); }

        private string _projectCode = string.Empty;
        public string ProjectCode { get => _projectCode; set => SetProperty(ref _projectCode, value); }

        private string _projectTitle = string.Empty;
        public string ProjectTitle { get => _projectTitle; set => SetProperty(ref _projectTitle, value); }

        private string _projectDescription = string.Empty;
        public string ProjectDescription { get => _projectDescription; set => SetProperty(ref _projectDescription, value); }

        private int? _selectedSourceFundId;
        public int? SelectedSourceFundId 
        { 
            get => _selectedSourceFundId; 
            set 
            {
                SetProperty(ref _selectedSourceFundId, value);
                var fund = SourceFunds.FirstOrDefault(f => f.SourceFundId == value);
                SelectedSourceFundName = fund?.FundName;
                RemainingFundBalance = fund != null ? fund.RemainingAmount : 0;
            } 
        }

        private string? _selectedSourceFundName;
        public string? SelectedSourceFundName { get => _selectedSourceFundName; set => SetProperty(ref _selectedSourceFundName, value); }

        private decimal _remainingFundBalance;
        public decimal RemainingFundBalance { get => _remainingFundBalance; set => SetProperty(ref _remainingFundBalance, value); }

        private decimal _amountPerBeneficiary;
        public decimal AmountPerBeneficiary { get => _amountPerBeneficiary; set => SetProperty(ref _amountPerBeneficiary, value); }

        // Program/track selector — filters the board to a single beneficiary program.
        public ObservableCollection<string> Programs { get; } = new()
        {
            "Job Order", "Casual", "Regular", "Captain"
        };

        // Captured in the Confirm Batch dialog; labels the batch and feeds the GGMS
        // programType. Changing it must never reload the board — that would discard
        // release/hold/reject decisions the registrar has already made.
        private string _selectedProgram = "Job Order";
        public string SelectedProgram
        {
            get => _selectedProgram;
            set => SetProperty(ref _selectedProgram, value);
        }

        private string _searchQuery = string.Empty;
        public string SearchQuery { get => _searchQuery; set => SetProperty(ref _searchQuery, value); }

        // Status properties
        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private string _statusIconKind = "CheckCircle";
        public string StatusIconKind { get => _statusIconKind; set => SetProperty(ref _statusIconKind, value); }

        private string _statusIconColor = "#166534";
        public string StatusIconColor { get => _statusIconColor; set => SetProperty(ref _statusIconColor, value); }

        public ObservableCollection<SourceFund> SourceFunds { get; } = new();

        public ObservableCollection<DistributionRecordViewModel> ReleasedRecords { get; } = new();
        public ObservableCollection<DistributionRecordViewModel> PendingRecords { get; } = new();
        public ObservableCollection<DistributionRecordViewModel> UnreleasedRecords { get; } = new();
        public ObservableCollection<DistributionRecordViewModel> RejectedRecords { get; } = new();

        // Drive the per-column empty-state illustrations.
        public bool HasReleasedRecords => ReleasedRecords.Count > 0;
        public bool HasUnreleasedRecords => UnreleasedRecords.Count > 0;
        public bool HasPendingRecords => PendingRecords.Count > 0;
        public bool HasRejectedRecords => RejectedRecords.Count > 0;

        public IAsyncRelayCommand LoadDataCommand { get; }
        public IAsyncRelayCommand SearchCommand { get; }
        public IAsyncRelayCommand PreloadBeneficiariesCommand { get; }
        public IRelayCommand<DistributionRecordViewModel> UndoRecordCommand { get; }
        public IAsyncRelayCommand<DistributionRecordViewModel> ApproveRecordCommand { get; }
        public IAsyncRelayCommand<DistributionRecordViewModel> RejectRecordCommand { get; }
        public IAsyncRelayCommand ConfirmBatchCommand { get; }
        public IAsyncRelayCommand ScanCameraCommand { get; }
        
        // Placeholders for top action buttons
        public IRelayCommand RenameBatchCommand { get; }
        public IRelayCommand ReuseBatchCommand { get; }
        public IRelayCommand DeleteBatchCommand { get; }
        public IRelayCommand ViewFundCommand { get; }

        public DistributionBatchViewModel(eSureHiDbContext dbContext, DistributionBoardMode? mode = null)
        {
            _dbContext = dbContext;
            Mode = mode ?? (PermissionService.IsUserRegistrar(AuthService.Instance.CurrentUser?.Role)
                ? DistributionBoardMode.Registrar
                : DistributionBoardMode.AdminHistory);

            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            SearchCommand = new AsyncRelayCommand(SearchAsync);
            PreloadBeneficiariesCommand = new AsyncRelayCommand(PreloadBeneficiariesAsync);

            UndoRecordCommand = new RelayCommand<DistributionRecordViewModel>(UndoRecord);

            // Mirrors the per-row review pattern used by PaymentsViewModel: the record must
            // actually be Pending, and the operator must hold the review permission.
            ApproveRecordCommand = new AsyncRelayCommand<DistributionRecordViewModel>(
                ApproveRecordAsync,
                r => r?.Status == "Pending" && PermissionService.CanApproveWorkflow);
            RejectRecordCommand = new AsyncRelayCommand<DistributionRecordViewModel>(
                RejectRecordAsync,
                r => r?.Status == "Pending" && PermissionService.CanReject);

            ConfirmBatchCommand = new AsyncRelayCommand(ConfirmBatchAsync);
            ScanCameraCommand = new AsyncRelayCommand(ScanCameraAsync);

            RenameBatchCommand = new RelayCommand(() => MessageBox.Show("Rename Batch not implemented yet."));
            ReuseBatchCommand = new RelayCommand(() => MessageBox.Show("Reuse Batch not implemented yet."));
            DeleteBatchCommand = new RelayCommand(() => MessageBox.Show("Delete Batch not implemented yet."));
            ViewFundCommand = new RelayCommand(() => MessageBox.Show("View Fund not implemented yet."));
        }

        private async Task LoadDataAsync()
        {
            var funds = await _dbContext.SourceFunds.ToListAsync();
            SourceFunds.Clear();
            foreach (var fund in funds)
            {
                SourceFunds.Add(fund);
            }

            TotalFunds = funds
                .Where(f => f.Status == "Active")
                .Sum(f => f.AllocatedAmount - f.UsedAmount);

            if (IsAdminHistoryMode)
            {
                await LoadHistoryAsync();
            }
            else
            {
                await PreloadBeneficiariesAsync();
            }
        }

        /// <summary>
        /// Admin mode: read-only snapshot of persisted records, loaded once on page open.
        /// </summary>
        private async Task LoadHistoryAsync()
        {
            var records = await _dbContext.DistributionRecords
                .Include(r => r.Batch)
                .Include(r => r.Beneficiary)
                .Where(r => r.Status == "Released" || r.Status == "Pending" || r.Status == "Rejected")
                .OrderByDescending(r => r.ProcessedAt)
                .ToListAsync();

            ReleasedRecords.Clear();
            PendingRecords.Clear();
            UnreleasedRecords.Clear();
            RejectedRecords.Clear();

            foreach (var r in records)
            {
                var vm = new DistributionRecordViewModel(r);
                if (r.Status == "Released") ReleasedRecords.Add(vm);
                else if (r.Status == "Rejected") RejectedRecords.Add(vm);
                else PendingRecords.Add(vm);
            }

            UpdateCounts();
        }

        private async Task PreloadBeneficiariesAsync()
        {
            // Get beneficiary IDs already fully released in a past confirmed batch
            var alreadyReleasedIds = await _dbContext.DistributionRecords
                .Where(r => r.Status == "Released")
                .Select(r => r.BeneficiaryId)
                .Distinct()
                .ToListAsync();

            // The queue lists every active beneficiary still awaiting release. Program is
            // batch metadata captured at Confirm time, not a board filter.
            var beneficiaries = await _dbContext.Beneficiaries
                .Where(b => b.IsActive && !alreadyReleasedIds.Contains(b.BenId))
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            UnreleasedRecords.Clear();
            ReleasedRecords.Clear();
            PendingRecords.Clear();
            RejectedRecords.Clear();

            foreach (var b in beneficiaries)
            {
                var record = new DistributionRecord
                {
                    BeneficiaryId = b.BenId,
                    Beneficiary = b,
                    Status = "Unreleased",
                    Remarks = "Waiting to claim"
                };
                UnreleasedRecords.Add(new DistributionRecordViewModel(record));
            }
            UpdateCounts();
        }

        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery)) return;

            var q = SearchQuery.Trim().ToLower();

            var allRecords = UnreleasedRecords.Concat(PendingRecords).Concat(ReleasedRecords).Concat(RejectedRecords).ToList();

            var found = allRecords.FirstOrDefault(r => r.BeneficiaryId.ToLower() == q)
                ?? allRecords.FirstOrDefault(r =>
                    r.BeneficiaryName.ToLower().Contains(q) ||
                    r.BeneficiaryId.ToLower().Contains(q));

            if (found == null)
            {
                SetStatus("Beneficiary not found in this batch.", "AlertCircle", "#991B1B");
                return;
            }

            SearchQuery = string.Empty;

            // Admin mode is a read-only snapshot — locate only, never open the processing dialog.
            if (IsAdminHistoryMode)
            {
                SetStatus($"{found.BeneficiaryName} — {found.Status}", "InformationOutline", "#1D4ED8");
                return;
            }

            await ShowIdCardAndProcessAsync(found);
        }

        /// <summary>
        /// Opens the digital ID card popup for the matched beneficiary. The card runs the
        /// monthly-limit / duplicate-release check and lets the operator Release (if eligible)
        /// or move the record to On Hold.
        /// </summary>
        private async Task ShowIdCardAndProcessAsync(DistributionRecordViewModel found)
        {
            var cardVm = new DistributionIdCardViewModel(found.Model.BeneficiaryId);
            await cardVm.LoadAsync();

            var dialog = new DistributionIdCardDialog(cardVm);
            var result = await DialogHost.Show(dialog, "DistributionDialogHost");

            var action = result as string;
            if (action == "Release")
            {
                ChangeStatus(found, "Released", "Claimed");
                SetStatus($"✓ Released — {found.BeneficiaryName}", "CheckCircle", "#166534");
            }
            else if (action == "Hold")
            {
                var remark = string.IsNullOrWhiteSpace(cardVm.HoldReason)
                    ? "Placed on hold for review."
                    : cardVm.HoldReason;
                ChangeStatus(found, "Pending", remark);
                SetStatus($"⚠ {found.BeneficiaryName} moved to On Hold.", "AlertCircle", "#991B1B");
            }
            else if (action == "Reject")
            {
                var remark = string.IsNullOrWhiteSpace(cardVm.HoldReason)
                    ? "Rejected — not eligible for this distribution."
                    : cardVm.HoldReason;
                ChangeStatus(found, "Rejected", remark);
                SetStatus($"✕ {found.BeneficiaryName} rejected.", "CloseCircle", "#B91C1C");
            }
            else
            {
                SetStatus($"Cancelled — {found.BeneficiaryName} left unchanged.", "InformationOutline", "#64748B");
            }
        }

        private async Task ScanCameraAsync()
        {
            try
            {
                var dialog = new QRScannerDialog();
                dialog.QRCodeScanned += (scannedId) =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SearchQuery = scannedId;
                        SearchCommand.Execute(null);
                    });
                };
                await DialogHost.Show(dialog, "DistributionDialogHost");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Camera scanning is unavailable because a required component failed to load: {ex.Message}",
                    "Camera Unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Admin approves a Pending record: it becomes Released and the beneficiary's share
        /// is debited from the fund that its batch was drawn against. The debit lives here
        /// rather than at batch-confirm time because approval is what decides a release.
        /// </summary>
        private async Task ApproveRecordAsync(DistributionRecordViewModel? record)
        {
            if (record == null || record.Status != "Pending") return;
            if (!PermissionService.CanApproveWorkflow)
            {
                SetStatus("You do not have permission to approve releases.", "LockOutline", "#991B1B");
                return;
            }

            ChangeStatus(record, "Released", "Claimed");

            var batch = record.Model.Batch;
            if (batch?.SourceFundId != null && batch.AmountPerBeneficiary > 0)
            {
                var fund = await _dbContext.SourceFunds.FindAsync(batch.SourceFundId);
                if (fund != null) fund.UsedAmount += batch.AmountPerBeneficiary;
            }

            await _dbContext.SaveChangesAsync();
            await RefreshTotalFundsAsync();

            SetStatus($"✓ Approved — {record.BeneficiaryName} released.", "CheckCircle", "#166534");
        }

        /// <summary>
        /// Admin rejects a Pending record — typically because a household member already
        /// claimed. Captures a reason in a small confirm dialog (not the full ID card) and
        /// moves the record straight out of Pending into Rejected.
        /// </summary>
        private async Task RejectRecordAsync(DistributionRecordViewModel? record)
        {
            if (record == null || record.Status != "Pending") return;
            if (!PermissionService.CanReject)
            {
                SetStatus("You do not have permission to reject releases.", "LockOutline", "#991B1B");
                return;
            }

            var rejectVm = new RejectRecordViewModel(record.BeneficiaryName);
            var dialog = new RejectRecordDialog(rejectVm);
            var verdict = await DialogHost.Show(dialog, "DistributionDialogHost") as string;
            if (verdict != "Reject") return;

            var reason = string.IsNullOrWhiteSpace(rejectVm.Reason)
                ? "Rejected by reviewer."
                : rejectVm.Reason.Trim();

            ChangeStatus(record, "Rejected", reason);
            await _dbContext.SaveChangesAsync();

            SetStatus($"✕ Rejected — {record.BeneficiaryName}. {reason}", "CloseCircle", "#B91C1C");
        }

        private async Task RefreshTotalFundsAsync()
        {
            TotalFunds = await _dbContext.SourceFunds
                .Where(f => f.Status == "Active")
                .SumAsync(f => f.AllocatedAmount - f.UsedAmount);
        }

        private void UndoRecord(DistributionRecordViewModel? record)
        {
            if (record != null)
            {
                ChangeStatus(record, "Unreleased", "Waiting to claim");
                SetStatus($"Undo action for {record.BeneficiaryName}", "Undo", "#64748B");
            }
        }

        private void ChangeStatus(DistributionRecordViewModel record, string newStatus, string remarks)
        {
            if (record.Status == newStatus) return;

            if (record.Status == "Unreleased") UnreleasedRecords.Remove(record);
            else if (record.Status == "Pending") PendingRecords.Remove(record);
            else if (record.Status == "Released") ReleasedRecords.Remove(record);
            else if (record.Status == "Rejected") RejectedRecords.Remove(record);

            record.Status = newStatus;
            record.Remarks = remarks;
            record.Model.ProcessedAt = DateTime.Now;

            if (newStatus == "Unreleased") UnreleasedRecords.Add(record);
            else if (newStatus == "Pending") PendingRecords.Add(record);
            else if (newStatus == "Released") ReleasedRecords.Add(record);
            else if (newStatus == "Rejected") RejectedRecords.Add(record);

            UpdateCounts();
        }

        private void UpdateCounts()
        {
            OnPropertyChanged(nameof(ReleasedRecords));
            OnPropertyChanged(nameof(UnreleasedRecords));
            OnPropertyChanged(nameof(PendingRecords));
            OnPropertyChanged(nameof(RejectedRecords));
            OnPropertyChanged(nameof(HasReleasedRecords));
            OnPropertyChanged(nameof(HasUnreleasedRecords));
            OnPropertyChanged(nameof(HasPendingRecords));
            OnPropertyChanged(nameof(HasRejectedRecords));

            // A record that just left Pending must stop offering Approve/Reject.
            ApproveRecordCommand.NotifyCanExecuteChanged();
            RejectRecordCommand.NotifyCanExecuteChanged();
        }

        private void SetStatus(string message, string iconKind, string iconColor)
        {
            StatusMessage = message;
            StatusIconKind = iconKind;
            StatusIconColor = iconColor;
        }

        private async Task ConfirmBatchAsync()
        {
            // Program / fund / amount are captured here rather than on the board header.
            var dialog = new ConfirmBatchDialog(this);
            var verdict = await DialogHost.Show(dialog, "DistributionDialogHost") as string;
            if (verdict != "Confirm") return;

            if (SelectedSourceFundId == null)
            {
                SetStatus("Please select a Source of Fund.", "AlertCircle", "#991B1B");
                return;
            }

            if (AmountPerBeneficiary <= 0)
            {
                SetStatus("Please enter an amount per beneficiary.", "AlertCircle", "#991B1B");
                return;
            }

            // GGMS sync sends ProjectTitle as programType — fall back to the chosen program.
            if (string.IsNullOrWhiteSpace(ProjectTitle))
                ProjectTitle = SelectedProgram;

            var batch = new DistributionBatch
            {
                ProjectCode = ProjectCode,
                ProjectTitle = ProjectTitle,
                ProjectDescription = ProjectDescription,
                SourceFundId = SelectedSourceFundId,
                AmountPerBeneficiary = AmountPerBeneficiary
            };

            _dbContext.DistributionBatches.Add(batch);

            var fund = await _dbContext.SourceFunds.FindAsync(SelectedSourceFundId);

            var allRecords = UnreleasedRecords.Concat(PendingRecords).Concat(ReleasedRecords).Concat(RejectedRecords).ToList();
            foreach (var r in allRecords)
            {
                r.Model.Batch = batch;
                _dbContext.DistributionRecords.Add(r.Model);

                if (r.Status == "Released" && fund != null)
                {
                    fund.UsedAmount += AmountPerBeneficiary;
                }
            }

            await _dbContext.SaveChangesAsync();

            // Sync to GGMS
            bool ggmsSuccess = true;
            string ggmsErrors = string.Empty;
            int syncCount = 0;

            if (fund != null)
            {
                var releasedItems = allRecords.Where(r => r.Status == "Released" && r.Model.Beneficiary != null).ToList();
                foreach (var r in releasedItems)
                {
                    var beneficiary = r.Model.Beneficiary!;
                    
                    // Fetch middle name from crs_beneficiary_cache
                    var middleName = await _dbContext.CrsBeneficiaryCache
                        .Where(c => c.BeneficiaryId == beneficiary.BeneficiaryId)
                        .Select(c => c.MiddleName)
                        .FirstOrDefaultAsync();

                    var (success, msg) = await eSureHi.Services.GgmsService.RecordDistributionReleaseAsync(
                        distributionRecordId: r.Model.RecordId,
                        beneficiaryIdentity: beneficiary.BeneficiaryId,
                        civilRegistryId: beneficiary.CivilRegistryId,
                        amountReleased: AmountPerBeneficiary,
                        programType: ProjectTitle,
                        firstName: beneficiary.FirstName,
                        middleName: middleName,
                        lastName: beneficiary.LastName,
                        fullName: beneficiary.FullName,
                        batchName: ProjectTitle,
                        sourceOfFunds: fund.FundName
                    );

                    if (success)
                    {
                        syncCount++;
                    }
                    else
                    {
                        ggmsSuccess = false;
                        ggmsErrors += $"\n- {beneficiary.FullName}: {msg}";
                    }
                }
            }

            if (ggmsSuccess)
            {
                SetStatus($"Distribution batch saved successfully. Synced {syncCount} transactions to GGMS.", "CheckCircle", "#166534");
            }
            else
            {
                SetStatus($"Distribution batch saved locally. GGMS Sync issues:{ggmsErrors}", "AlertCircle", "#991B1B");
            }
        }
    }
}
