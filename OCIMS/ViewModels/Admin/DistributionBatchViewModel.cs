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

        /// <summary>
        /// True once this record's share has been debited from a source fund — either at
        /// Confirm Batch time (registrar) or on Admin approval. Persisted on the model
        /// (distribution_records.fund_debited) so the guard holds across sessions: a batch
        /// confirmed in one session can't be re-charged when Admin approves it in another.
        /// </summary>
        public bool FundDebited
        {
            get => _model.FundDebited;
            set => _model.FundDebited = value;
        }

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

    public class DistributionBatchViewModel : ObservableObject
    {
        private readonly eSureHiDbContext _dbContext;
        private DistributionBatch? _currentBatch;

        /// <summary>
        /// Per-row Approve/Reject buttons on the On Hold Pending column. The board itself
        /// is identical for every role with distribution access; only these actions stay
        /// gated, because deciding a release is a reviewer's call. The User (registrar)
        /// role holds and rejects at the ID card, and files claims elsewhere.
        /// </summary>
        public bool ShowRowReviewActions => PermissionService.CanApproveWorkflow || PermissionService.CanReject;

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
        public decimal AmountPerBeneficiary 
        { 
            get => _amountPerBeneficiary; 
            set 
            {
                if (SetProperty(ref _amountPerBeneficiary, value))
                {
                    OnPropertyChanged(nameof(TotalReleasedAmount));
                    OnPropertyChanged(nameof(TotalPendingAmount));
                    OnPropertyChanged(nameof(TotalUnreleasedAmount));
                }
            } 
        }

        public decimal TotalReleasedAmount => ReleasedRecords.Sum(r => r.Model.Batch?.AmountPerBeneficiary ?? AmountPerBeneficiary);
        public decimal TotalPendingAmount => PendingRecords.Sum(r => r.Model.Batch?.AmountPerBeneficiary ?? AmountPerBeneficiary);
        public decimal TotalUnreleasedAmount => UnreleasedRecords.Sum(r => r.Model.Batch?.AmountPerBeneficiary ?? AmountPerBeneficiary);

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

        // Drive the per-column empty-state illustrations.
        public bool HasReleasedRecords => ReleasedRecords.Count > 0;
        public bool HasUnreleasedRecords => UnreleasedRecords.Count > 0;
        public bool HasPendingRecords => PendingRecords.Count > 0;

        public IAsyncRelayCommand LoadDataCommand { get; }
        public IAsyncRelayCommand SearchCommand { get; }
        public IAsyncRelayCommand PreloadBeneficiariesCommand { get; }
        public IRelayCommand<DistributionRecordViewModel> UndoRecordCommand { get; }
        public IAsyncRelayCommand<DistributionRecordViewModel> ApproveRecordCommand { get; }
        public IAsyncRelayCommand ConfirmBatchCommand { get; }
        public IAsyncRelayCommand ScanCameraCommand { get; }
        
        // Placeholders for top action buttons
        public IRelayCommand RenameBatchCommand { get; }
        public IRelayCommand ReuseBatchCommand { get; }
        public IRelayCommand DeleteBatchCommand { get; }
        public IRelayCommand ViewFundCommand { get; }

        public DistributionBatchViewModel(eSureHiDbContext dbContext)
        {
            _dbContext = dbContext;

            LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
            SearchCommand = new AsyncRelayCommand(SearchAsync);
            PreloadBeneficiariesCommand = new AsyncRelayCommand(PreloadBeneficiariesAsync);

            UndoRecordCommand = new RelayCommand<DistributionRecordViewModel>(UndoRecord);

            // Mirrors the per-row review pattern used by PaymentsViewModel: the record must
            // actually be Pending, and the operator must hold the review permission.
            ApproveRecordCommand = new AsyncRelayCommand<DistributionRecordViewModel>(
                ApproveRecordAsync,
                r => r?.Status == "Pending" && PermissionService.CanApproveWorkflow);

            ConfirmBatchCommand = new AsyncRelayCommand(ConfirmBatchAsync);
            ScanCameraCommand = new AsyncRelayCommand(ScanCameraAsync);

            RenameBatchCommand = new RelayCommand(() => MessageBox.Show("Rename Batch not implemented yet."));
            ReuseBatchCommand = new RelayCommand(() => MessageBox.Show("Reuse Batch not implemented yet."));
            DeleteBatchCommand = new RelayCommand(() => MessageBox.Show("Delete Batch not implemented yet."));
            ViewFundCommand = new RelayCommand(() => MessageBox.Show("View Fund not implemented yet."));
        }

        private async Task LoadDataAsync()
        {
            try
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

                await PreloadBeneficiariesAsync();
            }
            catch (Exception ex)
            {
                // Loaded via the view's Loaded trigger under AsyncRelayCommand, which has no
                // error wrapper — without this catch a DB outage shows an empty board silently.
                SetStatus($"Could not load the distribution board — {ex.Message}", "AlertCircle", "#991B1B");
            }
        }

        /// <summary>
        /// Fills the board from two sources at once: persisted Pending/Rejected records so
        /// in-flight review items survive a restart, plus a live queue of every active
        /// beneficiary not yet accounted for. A beneficiary already carried by a persisted
        /// record is excluded from the queue so they can't appear in two columns.
        /// </summary>
        private async Task PreloadBeneficiariesAsync()
        {
            try
            {
                _currentBatch = await _dbContext.DistributionBatches
                    .OrderByDescending(b => b.CreatedAt)
                    .FirstOrDefaultAsync();

                if (_currentBatch != null)
                {
                    SelectedSourceFundId = _currentBatch.SourceFundId;
                    AmountPerBeneficiary = _currentBatch.AmountPerBeneficiary;
                }

                var persisted = await _dbContext.DistributionRecords
                    .Include(r => r.Batch)
                    .Include(r => r.Beneficiary)
                    .Where(r => r.Status == "Released" || r.Status == "Pending")
                    .OrderByDescending(r => r.ProcessedAt)
                    .ToListAsync();

                // Anyone with a persisted record of any kind is already represented; the
                // queue only offers people with no distribution outcome on file yet.
                var spokenForIds = persisted.Select(r => r.BeneficiaryId).Distinct().ToList();

                var beneficiaries = await _dbContext.Beneficiaries
                    .Where(b => b.IsActive && !spokenForIds.Contains(b.BenId))
                    .OrderByDescending(b => b.CreatedAt)
                    .ToListAsync();

                UnreleasedRecords.Clear();
                ReleasedRecords.Clear();
                PendingRecords.Clear();

                foreach (var r in persisted)
                {
                    var vm = new DistributionRecordViewModel(r);
                    if (r.Status == "Released") ReleasedRecords.Add(vm);
                    else PendingRecords.Add(vm);
                }

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
            catch (Exception ex)
            {
                // Also reachable directly through PreloadBeneficiariesCommand, so it needs
                // its own guard rather than relying on LoadDataAsync's.
                SetStatus($"Could not load the beneficiary queue — {ex.Message}", "AlertCircle", "#991B1B");
            }
        }

        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery)) return;

            var q = SearchQuery.Trim().ToLower();

            var allRecords = UnreleasedRecords.Concat(PendingRecords).Concat(ReleasedRecords).ToList();

            var foundExact = allRecords.FirstOrDefault(r => r.BeneficiaryId.ToLower() == q);
            var found = foundExact ?? allRecords.FirstOrDefault(r =>
                r.BeneficiaryName.ToLower().Contains(q) ||
                r.BeneficiaryId.ToLower().Contains(q));

            if (found == null)
            {
                SetStatus("Beneficiary not found in this batch.", "AlertCircle", "#991B1B");
                return;
            }

            bool isExactIdMatch = foundExact != null && found == foundExact;

            SearchQuery = string.Empty;

            await ShowIdCardAndProcessAsync(found, isExactIdMatch);
        }

        /// <summary>
        /// Opens the digital ID card popup for the matched beneficiary. The card runs the
        /// monthly-limit / duplicate-release check and lets the operator Release (if eligible)
        /// or move the record to On Hold.
        /// </summary>
        private async Task ShowIdCardAndProcessAsync(DistributionRecordViewModel found, bool isExactIdMatch = false)
        {
            var cardVm = new DistributionIdCardViewModel(found.Model.BeneficiaryId, found.Status);
            await cardVm.LoadAsync();

            if (isExactIdMatch && found.Status == "Unreleased" && cardVm.IsEligible)
            {
                ChangeStatus(found, "Released", "Claimed");
                cardVm.SetReleasedVerdict("AUTOMATICALLY RELEASED — claim verified, benefit released.");
                SetStatus($"✓ Automatically Released — {found.BeneficiaryName}", "CheckCircle", "#166534");
                await SaveSingleRecordAsync(found);
            }

            var dialog = new DistributionIdCardDialog(cardVm);
            var result = await DialogHost.Show(dialog, "DistributionDialogHost");

            var action = result as string;
            if (action == "Release")
            {
                ChangeStatus(found, "Released", "Claimed");
                SetStatus($"✓ Released — {found.BeneficiaryName}", "CheckCircle", "#166534");
                await SaveSingleRecordAsync(found);
            }
            else if (action == "Hold")
            {
                var remark = string.IsNullOrWhiteSpace(cardVm.HoldReason)
                    ? "Placed on hold for review."
                    : cardVm.HoldReason;
                ChangeStatus(found, "Pending", remark);
                SetStatus($"⚠ {found.BeneficiaryName} moved to On Hold.", "AlertCircle", "#991B1B");
                await SaveSingleRecordAsync(found);
            }
            else
            {
                if (found.Status != "Released")
                {
                    SetStatus($"Cancelled — {found.BeneficiaryName} left unchanged.", "InformationOutline", "#64748B");
                }
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

        private async Task ApproveRecordAsync(DistributionRecordViewModel? record)
        {
            if (record == null || record.Status != "Pending") return;
            if (!PermissionService.CanApproveWorkflow)
            {
                SetStatus("You do not have permission to approve releases.", "LockOutline", "#991B1B");
                return;
            }

            // Check if beneficiary has filed a claim
            var hasClaim = await _dbContext.Claims.AnyAsync(c => c.BenId == record.Model.BeneficiaryId);
            if (!hasClaim)
            {
                SetStatus($"Cannot release — {record.BeneficiaryName} has not filed a claim yet.", "AlertCircle", "#991B1B");
                return;
            }

            var originalStatus = record.Status;
            var originalRemarks = record.Remarks ?? string.Empty;
            var originalProcessedAt = record.Model.ProcessedAt;

            var batch = record.Model.Batch;
            SourceFund? fund = null;
            bool debitedHere = false;

            try
            {
                ChangeStatus(record, "Released", "Claimed");

                if (!record.FundDebited && batch?.SourceFundId != null && batch.AmountPerBeneficiary > 0)
                {
                    fund = await _dbContext.SourceFunds.FindAsync(batch.SourceFundId);
                    if (fund != null)
                    {
                        fund.UsedAmount += batch.AmountPerBeneficiary;
                        record.FundDebited = true;
                        debitedHere = true;
                    }
                }

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Undo the debit and the status flip so the shared context holds no
                // stale changes that a later, unrelated SaveChanges could commit.
                if (debitedHere && fund != null && batch != null)
                {
                    fund.UsedAmount -= batch.AmountPerBeneficiary;
                    record.FundDebited = false;
                }
                ChangeStatus(record, originalStatus, originalRemarks);
                record.Model.ProcessedAt = originalProcessedAt;
                SetStatus($"Approve failed — nothing was saved. {ex.Message}", "AlertCircle", "#991B1B");
                return;
            }

            await RefreshTotalFundsAsync();

            SetStatus($"✓ Approved — {record.BeneficiaryName} released.", "CheckCircle", "#166534");
        }



        private async Task RefreshTotalFundsAsync()
        {
            try
            {
                TotalFunds = await _dbContext.SourceFunds
                    .Where(f => f.Status == "Active")
                    .SumAsync(f => f.AllocatedAmount - f.UsedAmount);
            }
            catch
            {
                // Cosmetic stat only; the approve/reject that triggered the refresh already
                // saved. A stale total beats reporting a phantom failure.
            }
        }

        private async void UndoRecord(DistributionRecordViewModel? record)
        {
            if (record != null)
            {
                var originalStatus = record.Status;
                var originalProcessedAt = record.Model.ProcessedAt;
                var originalRemarks = record.Remarks;

                ChangeStatus(record, "Unreleased", "Waiting to claim");

                try
                {
                    var dbRecord = await _dbContext.DistributionRecords.FirstOrDefaultAsync(r => r.RecordId == record.Model.RecordId);
                    if (dbRecord != null)
                    {
                        if (record.FundDebited && _currentBatch != null)
                        {
                            var fund = await _dbContext.SourceFunds.FindAsync(_currentBatch.SourceFundId);
                            if (fund != null)
                            {
                                fund.UsedAmount -= _currentBatch.AmountPerBeneficiary;
                                record.FundDebited = false;
                            }
                        }

                        _dbContext.DistributionRecords.Remove(dbRecord);
                        await _dbContext.SaveChangesAsync();
                        record.Model.RecordId = 0;
                    }
                    SetStatus($"Undo action for {record.BeneficiaryName}", "Undo", "#64748B");
                }
                catch (Exception ex)
                {
                    ChangeStatus(record, originalStatus, originalRemarks ?? string.Empty);
                    record.Model.ProcessedAt = originalProcessedAt;
                    MessageBox.Show($"Failed to undo release in database: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task SaveSingleRecordAsync(DistributionRecordViewModel record)
        {
            try
            {
                if (SelectedSourceFundId == null)
                {
                    var defaultFund = await _dbContext.SourceFunds
                        .FirstOrDefaultAsync(f => f.Status == "Active" && f.FundName == SelectedProgram);
                    if (defaultFund != null)
                    {
                        SelectedSourceFundId = defaultFund.SourceFundId;
                    }
                    else
                    {
                        var firstActive = await _dbContext.SourceFunds.FirstOrDefaultAsync(f => f.Status == "Active");
                        if (firstActive != null)
                        {
                            SelectedSourceFundId = firstActive.SourceFundId;
                        }
                    }
                }

                if (AmountPerBeneficiary <= 0)
                {
                    AmountPerBeneficiary = 5000;
                }

                if (_currentBatch == null)
                {
                    _currentBatch = new DistributionBatch
                    {
                        ProjectCode = $"DIST-{DateTime.Now:yyyyMMddHHmmss}",
                        ProjectTitle = $"{SelectedProgram} Release — {DateTime.Today:MM/dd/yyyy}",
                        ProjectDescription = $"Automatic batch created for {SelectedProgram} distribution.",
                        SourceFundId = SelectedSourceFundId,
                        AmountPerBeneficiary = AmountPerBeneficiary
                    };
                    _dbContext.DistributionBatches.Add(_currentBatch);
                    await _dbContext.SaveChangesAsync();
                }
                else
                {
                    _currentBatch.SourceFundId = SelectedSourceFundId;
                    _currentBatch.AmountPerBeneficiary = AmountPerBeneficiary;
                }

                record.Model.BatchId = _currentBatch.BatchId;
                record.Model.Batch = _currentBatch;

                var dbRecord = await _dbContext.DistributionRecords
                    .FirstOrDefaultAsync(r => r.RecordId == record.Model.RecordId || 
                                             (r.BeneficiaryId == record.Model.BeneficiaryId && r.BatchId == _currentBatch.BatchId));

                if (dbRecord == null)
                {
                    _dbContext.DistributionRecords.Add(record.Model);
                }
                else
                {
                    dbRecord.Status = record.Status;
                    dbRecord.Remarks = record.Remarks;
                    dbRecord.ProcessedAt = record.Model.ProcessedAt;
                }

                if (record.Status == "Released" && !record.FundDebited && SelectedSourceFundId != null)
                {
                    var fund = await _dbContext.SourceFunds.FindAsync(SelectedSourceFundId);
                    if (fund != null)
                    {
                        fund.UsedAmount += AmountPerBeneficiary;
                        record.FundDebited = true;
                    }
                }

                await _dbContext.SaveChangesAsync();

                if (dbRecord == null)
                {
                    var savedRecord = await _dbContext.DistributionRecords
                        .FirstOrDefaultAsync(r => r.BeneficiaryId == record.Model.BeneficiaryId && r.BatchId == _currentBatch.BatchId);
                    if (savedRecord != null)
                    {
                        record.Model.RecordId = savedRecord.RecordId;
                    }
                }

                await RefreshTotalFundsAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Save failed: {ex.Message}", "AlertCircle", "#991B1B");
            }
        }

        private void ChangeStatus(DistributionRecordViewModel record, string newStatus, string remarks)
        {
            if (record.Status == newStatus) return;

            if (record.Status == "Unreleased") UnreleasedRecords.Remove(record);
            else if (record.Status == "Pending") PendingRecords.Remove(record);
            else if (record.Status == "Released") ReleasedRecords.Remove(record);

            record.Status = newStatus;
            record.Remarks = remarks;
            record.Model.ProcessedAt = DateTime.Now;

            if (newStatus == "Unreleased") UnreleasedRecords.Add(record);
            else if (newStatus == "Pending") PendingRecords.Add(record);
            else if (newStatus == "Released") ReleasedRecords.Add(record);

            UpdateCounts();
        }

        private void UpdateCounts()
        {
            OnPropertyChanged(nameof(ReleasedRecords));
            OnPropertyChanged(nameof(UnreleasedRecords));
            OnPropertyChanged(nameof(PendingRecords));
            OnPropertyChanged(nameof(HasReleasedRecords));
            OnPropertyChanged(nameof(HasUnreleasedRecords));
            OnPropertyChanged(nameof(HasPendingRecords));

            OnPropertyChanged(nameof(TotalReleasedAmount));
            OnPropertyChanged(nameof(TotalPendingAmount));
            OnPropertyChanged(nameof(TotalUnreleasedAmount));

            // A record that just left Pending must stop offering Approve.
            ApproveRecordCommand.NotifyCanExecuteChanged();
        }

        private void SetStatus(string message, string iconKind, string iconColor)
        {
            StatusMessage = message;
            StatusIconKind = iconKind;
            StatusIconColor = iconColor;
        }

        private async Task ConfirmBatchAsync()
        {
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

            if (string.IsNullOrWhiteSpace(ProjectTitle))
                ProjectTitle = SelectedProgram;

            if (_currentBatch == null)
            {
                _currentBatch = new DistributionBatch
                {
                    ProjectCode = string.IsNullOrWhiteSpace(ProjectCode) ? $"DIST-{DateTime.Now:yyyyMMddHHmmss}" : ProjectCode,
                    ProjectTitle = ProjectTitle,
                    ProjectDescription = ProjectDescription,
                    SourceFundId = SelectedSourceFundId,
                    AmountPerBeneficiary = AmountPerBeneficiary
                };
                _dbContext.DistributionBatches.Add(_currentBatch);
            }
            else
            {
                _currentBatch.ProjectCode = string.IsNullOrWhiteSpace(ProjectCode) ? _currentBatch.ProjectCode : ProjectCode;
                _currentBatch.ProjectTitle = ProjectTitle;
                _currentBatch.ProjectDescription = ProjectDescription;
                _currentBatch.SourceFundId = SelectedSourceFundId;
                _currentBatch.AmountPerBeneficiary = AmountPerBeneficiary;
            }

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"Confirm Batch failed — nothing was saved. {ex.Message}", "AlertCircle", "#991B1B");
                return;
            }

            bool ggmsSuccess = true;
            string ggmsErrors = string.Empty;
            int syncCount = 0;

            var fund = await _dbContext.SourceFunds.FindAsync(SelectedSourceFundId);
            if (fund != null)
            {
                var releasedItems = ReleasedRecords
                    .Where(r => r.Model.BatchId == _currentBatch.BatchId && r.Model.Beneficiary != null)
                    .ToList();

                foreach (var r in releasedItems)
                {
                    var beneficiary = r.Model.Beneficiary!;
                    try
                    {
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
                    catch (Exception ex)
                    {
                        ggmsSuccess = false;
                        ggmsErrors += $"\n- {beneficiary.FullName}: {ex.Message}";
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
