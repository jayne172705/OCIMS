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
    public class PolicyFormViewModel : ObservableObject
    {
        // ── Mode ───────────────────────────────────────────────────────
        public bool IsEditMode { get; private set; }
        public string DialogTitle => IsEditMode ? "Edit Policy" : "New Policy";
        private int _editPolicyId;

        // ── Tab 1 — Policy Info ────────────────────────────────────────
        private string _policyCode = string.Empty;
        private string _policyName = string.Empty;
        private string _policyType = "Regular";
        private string _policyStatus = "Active";
        private decimal _coverageAmount;

        public string PolicyCode { get => _policyCode; set => SetProperty(ref _policyCode, value); }
        public string PolicyName { get => _policyName; set => SetProperty(ref _policyName, value); }
        public string PolicyType { get => _policyType; set => SetProperty(ref _policyType, value); }
        public string PolicyStatus { get => _policyStatus; set => SetProperty(ref _policyStatus, value); }
        public decimal CoverageAmount { get => _coverageAmount; set => SetProperty(ref _coverageAmount, value); }

        // ── Tab 2 — Provider ───────────────────────────────────────────
        private string _providerName = string.Empty;
        private string _providerContact = string.Empty;
        private DateTime? _effectiveDate;
        private DateTime? _expiryDate;
        private DateTime? _renewalDate;

        public string ProviderName { get => _providerName; set => SetProperty(ref _providerName, value); }
        public string ProviderContact { get => _providerContact; set => SetProperty(ref _providerContact, value); }
        public DateTime? EffectiveDate { get => _effectiveDate; set => SetProperty(ref _effectiveDate, value); }
        public DateTime? ExpiryDate { get => _expiryDate; set => SetProperty(ref _expiryDate, value); }
        public DateTime? RenewalDate { get => _renewalDate; set => SetProperty(ref _renewalDate, value); }

        // ── Tab 3 — Details ────────────────────────────────────────────
        private string _description = string.Empty;
        private string _termsConditions = string.Empty;

        public string Description { get => _description; set => SetProperty(ref _description, value); }
        public string TermsConditions { get => _termsConditions; set => SetProperty(ref _termsConditions, value); }

        // ── Static Lists ───────────────────────────────────────────────
        public string[] PolicyTypes { get; } =
            { "Job Order", "Casual", "Regular" };
        public string[] StatusOptions { get; } =
            { "Active", "Expired", "Lapsed", "Cancelled", "Renewal" };

        // ── State ──────────────────────────────────────────────────────
        private string _errorMessage = string.Empty;
        private bool _isBusy;

        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
        public bool IsBusy { get => _isBusy; set { SetProperty(ref _isBusy, value); SaveCommand.RaiseCanExecuteChanged(); } }

        // ── Commands ───────────────────────────────────────────────────
        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        // ── Callbacks ──────────────────────────────────────────────────
        public Action? CloseAction { get; set; }
        public Action? OnSaveSuccess { get; set; }

        // ── Constructor ────────────────────────────────────────────────
        public PolicyFormViewModel()
        {
            SaveCommand = new RelayCommand(async () => await SaveAsync(), () => !IsBusy);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
        }

        // ── Init New ───────────────────────────────────────────────────
        public void InitNew()
        {
            IsEditMode = false;
            PolicyCode = GeneratePolicyCode();
            EffectiveDate = DateTime.Today;
            ExpiryDate = DateTime.Today.AddYears(1);
            RenewalDate = DateTime.Today.AddYears(1).AddMonths(-1);
        }

        // ── Init Edit ──────────────────────────────────────────────────
        public async Task InitEditAsync(int policyId)
        {
            IsEditMode = true;
            _editPolicyId = policyId;

            using var db = eSureHiDbContextFactory.Create();
            var p = await db.InsurancePolicies.FindAsync(policyId);
            if (p is null) return;

            PolicyCode = p.PolicyCode;
            PolicyName = p.PolicyName;
            PolicyType = p.PolicyType;
            PolicyStatus = p.PolicyStatus;
            CoverageAmount = p.CoverageAmount;
            ProviderName = p.ProviderName ?? string.Empty;
            ProviderContact = p.ProviderContact ?? string.Empty;
            EffectiveDate = p.EffectiveDate.HasValue
                                  ? p.EffectiveDate.Value.ToDateTime(TimeOnly.MinValue) : null;
            ExpiryDate = p.ExpiryDate.HasValue
                                  ? p.ExpiryDate.Value.ToDateTime(TimeOnly.MinValue) : null;
            RenewalDate = p.RenewalDate.HasValue
                                  ? p.RenewalDate.Value.ToDateTime(TimeOnly.MinValue) : null;
            Description = p.Description ?? string.Empty;
            TermsConditions = p.TermsConditions ?? string.Empty;
        }

        // ── Validate ───────────────────────────────────────────────────
        private bool Validate()
        {
            if (string.IsNullOrWhiteSpace(PolicyName))
            { ErrorMessage = "Policy name is required."; return false; }
            if (string.IsNullOrWhiteSpace(PolicyType))
            { ErrorMessage = "Policy type is required."; return false; }
            if (EffectiveDate is null)
            { ErrorMessage = "Effective date is required."; return false; }
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
                    var p = await db.InsurancePolicies.FindAsync(_editPolicyId);
                    if (p is null) { ErrorMessage = "Policy not found."; return; }
                    MapToEntity(p);
                    p.UpdatedAt = DateTime.Now;
                    await db.SaveChangesAsync();
                    // After edit policy:
                    await AuditService.LogUpdate("insurance_policies", _editPolicyId,
                        $"Policy updated: {PolicyName}");
                }
                else
                {
                    bool codeExists = await db.InsurancePolicies
                        .AnyAsync(p => p.PolicyCode == PolicyCode.Trim());
                    if (codeExists) PolicyCode = GeneratePolicyCode();

                    var p = new InsurancePolicy
                    {
                        CreatedBy = AuthService.Instance.CurrentUser?.UserId,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    MapToEntity(p);
                    db.InsurancePolicies.Add(p);
                    await db.SaveChangesAsync();
                    // After new policy:
                    await AuditService.LogInsert("insurance_policies", p.PolicyId,
                        $"New policy created: {PolicyName}");
                }

                OnSaveSuccess?.Invoke();
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Save failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Map VM → Entity ────────────────────────────────────────────
        private void MapToEntity(InsurancePolicy p)
        {
            p.PolicyCode = PolicyCode.Trim();
            p.PolicyName = PolicyName.Trim();
            p.PolicyType = PolicyType;
            p.PolicyStatus = PolicyStatus;
            p.CoverageAmount = CoverageAmount;
            p.ProviderName = string.IsNullOrWhiteSpace(ProviderName) ? null : ProviderName.Trim();
            p.ProviderContact = string.IsNullOrWhiteSpace(ProviderContact) ? null : ProviderContact.Trim();
            p.EffectiveDate = EffectiveDate.HasValue
                                    ? DateOnly.FromDateTime(EffectiveDate.Value) : null;
            p.ExpiryDate = ExpiryDate.HasValue
                                    ? DateOnly.FromDateTime(ExpiryDate.Value) : null;
            p.RenewalDate = RenewalDate.HasValue
                                    ? DateOnly.FromDateTime(RenewalDate.Value) : null;
            p.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
            p.TermsConditions = string.IsNullOrWhiteSpace(TermsConditions) ? null : TermsConditions.Trim();
        }

        private static string GeneratePolicyCode()
            => $"POL-{DateTime.Now:yyMMddHHmmss}";
    }
}
