using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using eSureHi.Data;
using eSureHi.Models;
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

    public class DistributionBatchViewModel : ObservableObject
    {
        private readonly eSureHiDbContext _dbContext;

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

        private string _selectedProgram = "Job Order";
        public string SelectedProgram
        {
            get => _selectedProgram;
            set
            {
                if (SetProperty(ref _selectedProgram, value))
                {
                    _ = PreloadBeneficiariesAsync();
                }
            }
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

        public IAsyncRelayCommand LoadDataCommand { get; }
        public IAsyncRelayCommand SearchCommand { get; }
        public IAsyncRelayCommand PreloadBeneficiariesCommand { get; }
        public IRelayCommand<DistributionRecordViewModel> UndoRecordCommand { get; }
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

            await PreloadBeneficiariesAsync();
        }

        private async Task PreloadBeneficiariesAsync()
        {
            // Get beneficiary IDs already fully released in a past confirmed batch
            var alreadyReleasedIds = await _dbContext.DistributionRecords
                .Where(r => r.Status == "Released")
                .Select(r => r.BeneficiaryId)
                .Distinct()
                .ToListAsync();

            var beneficiaries = await _dbContext.Beneficiaries
                .Where(b => b.IsActive
                            && !alreadyReleasedIds.Contains(b.BenId)
                            && b.SourceOfFunds == SelectedProgram)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            UnreleasedRecords.Clear();
            ReleasedRecords.Clear();
            PendingRecords.Clear();

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

            var allRecords = UnreleasedRecords.Concat(PendingRecords).Concat(ReleasedRecords).ToList();

            var found = allRecords.FirstOrDefault(r =>
                r.BeneficiaryName.ToLower().Contains(q) ||
                r.BeneficiaryId.ToLower() == q);

            if (found == null)
            {
                SetStatus("Beneficiary not found in this batch.", "AlertCircle", "#991B1B");
                return;
            }

            SearchQuery = string.Empty;
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
        }

        private void SetStatus(string message, string iconKind, string iconColor)
        {
            StatusMessage = message;
            StatusIconKind = iconKind;
            StatusIconColor = iconColor;
        }

        private async Task ConfirmBatchAsync()
        {
            if (SelectedSourceFundId == null)
            {
                SetStatus("Please select a Source of Fund.", "AlertCircle", "#991B1B");
                return;
            }

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

            var allRecords = UnreleasedRecords.Concat(PendingRecords).Concat(ReleasedRecords).ToList();
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
