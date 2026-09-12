using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using eSureHi.Data;
using eSureHi.Models;
using eSureHi.Services;
using Microsoft.EntityFrameworkCore;

namespace eSureHi.Views.Admin.Dialogs
{
    public partial class MemberEditDialog : Window
    {
        private readonly int _beneficiaryId;

        public string Relationship { get; set; } = "Other";
        public string? Email { get; set; }
        public string? RecipientsInsurance { get; set; }
        public string? SourceOfFunds { get; set; }
        public string ContributionText { get; set; } = "0";
        public string? CedulaNo { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsMemberActive { get; set; }
        public bool Received { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public string[] RelationshipOptions { get; } =
            { "Spouse", "Child", "Parent", "Sibling", "Grandchild", "Other" };

        public MemberEditDialog(Beneficiary beneficiary)
        {
            _beneficiaryId = beneficiary.BenId;
            Relationship = string.IsNullOrWhiteSpace(beneficiary.Relationship) ? "Other" : beneficiary.Relationship;
            Email = beneficiary.Email;
            RecipientsInsurance = beneficiary.RecipientsInsurance;
            SourceOfFunds = beneficiary.SourceOfFunds;
            ContributionText = beneficiary.Contribution.ToString("0.##", CultureInfo.InvariantCulture);
            CedulaNo = beneficiary.CedulaNo;
            IsPrimary = beneficiary.IsPrimary;
            IsMemberActive = beneficiary.IsActive;
            Received = beneficiary.Received;

            InitializeComponent();
            DataContext = this;
        }

        public Action? OnSaveSuccess { get; set; }

        private async void Save_Click(object sender, RoutedEventArgs e) => await SaveAsync();

        private async Task SaveAsync()
        {
            ErrorMessage = string.Empty;
            if (!decimal.TryParse(ContributionText, NumberStyles.Number, CultureInfo.CurrentCulture, out var contribution) || contribution < 0)
            {
                ErrorMessage = "Contribution must be a valid amount of zero or more.";
                RefreshBindings();
                return;
            }

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var member = await db.Beneficiaries.FirstOrDefaultAsync(b => b.BenId == _beneficiaryId);
                if (member is null)
                {
                    ErrorMessage = "This member record no longer exists.";
                    RefreshBindings();
                    return;
                }

                member.Relationship = Relationship;
                member.Email = Clean(Email);
                member.RecipientsInsurance = Clean(RecipientsInsurance);
                member.SourceOfFunds = Clean(SourceOfFunds);
                member.Contribution = contribution;
                member.CedulaNo = Clean(CedulaNo);
                member.IsPrimary = IsPrimary;
                member.IsActive = IsMemberActive;
                member.Received = Received;
                await db.SaveChangesAsync();
                await AuditService.LogUpdate("beneficiaries", member.BenId, $"Updated insurance member: {member.FullName}");

                OnSaveSuccess?.Invoke();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not update member: {ex.GetBaseException().Message}";
                RefreshBindings();
            }
        }

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private void RefreshBindings()
        {
            DataContext = null;
            DataContext = this;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
