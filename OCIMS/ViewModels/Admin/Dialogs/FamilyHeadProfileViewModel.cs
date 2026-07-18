using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Models;
using eSureHi.Helpers;

namespace eSureHi.ViewModels.Admin.Dialogs
{
    public class ConsolidatedTransaction
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class FamilyHeadProfileViewModel
    {
        public Employee Head { get; }
        public ObservableCollection<Beneficiary> Dependents { get; } = new();
        public ObservableCollection<ConsolidatedTransaction> Transactions { get; } = new();

        public ICommand CloseCommand { get; }
        public Action? CloseRequested { get; set; }

        public FamilyHeadProfileViewModel(Employee head)
        {
            Head = head;
            CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
        }

        public async Task LoadAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.CreateCloud();
                
                // Load dependents
                var dependentsList = await db.Beneficiaries
                    .Where(b => b.EmpId == Head.EmpId)
                    .ToListAsync();

                Dependents.Clear();
                foreach (var dep in dependentsList)
                {
                    Dependents.Add(dep);
                }

                // Load transactions
                var transactionsList = new System.Collections.Generic.List<ConsolidatedTransaction>();

                // 1. Benefits (Claims)
                var benefits = await db.Benefits
                    .Include(b => b.EmployeePolicy)
                        .ThenInclude(ep => ep!.Policy)
                    .Where(b => b.EmployeePolicy != null && b.EmployeePolicy.EmpId == Head.EmpId)
                    .ToListAsync();
                
                foreach (var b in benefits)
                {
                    transactionsList.Add(new ConsolidatedTransaction
                    {
                        Date = b.CreatedAt,
                        Type = "Benefit / Claim",
                        Description = $"{b.BenefitType} - {b.EmployeePolicy?.Policy?.PolicyName}",
                        Amount = b.UsedBenefit,
                        Status = "Released"
                    });
                }

                // 2. Premiums
                var premiums = await db.Premiums
                    .Include(p => p.EmployeePolicy)
                        .ThenInclude(ep => ep!.Policy)
                    .Where(p => p.EmployeePolicy != null && p.EmployeePolicy.EmpId == Head.EmpId)
                    .ToListAsync();

                foreach (var p in premiums)
                {
                    transactionsList.Add(new ConsolidatedTransaction
                    {
                        Date = p.PaidDate.HasValue ? p.PaidDate.Value.ToDateTime(TimeOnly.MinValue) : p.DueDate.ToDateTime(TimeOnly.MinValue),
                        Type = "Premium",
                        Description = $"Premium for {p.EmployeePolicy?.Policy?.PolicyName}",
                        Amount = p.AmountPaid > 0 ? p.AmountPaid : p.TotalAmount,
                        Status = p.PaymentStatus
                    });
                }

                // 3. Document Transactions
                var documentIds = await db.Documents
                    .Where(d => d.EmpId == Head.EmpId)
                    .Select(d => d.DocumentId)
                    .ToListAsync();
                
                var docTransactions = await db.DocumentTransactions
                    .Include(t => t.Sender)
                    .Include(t => t.Receiver)
                    .Where(t => t.DocumentId.HasValue && documentIds.Contains(t.DocumentId.Value))
                    .ToListAsync();

                foreach (var t in docTransactions)
                {
                    transactionsList.Add(new ConsolidatedTransaction
                    {
                        Date = t.CreatedAt,
                        Type = "Document",
                        Description = $"{t.TransactionType} - {t.Subject}",
                        Amount = 0,
                        Status = t.Status
                    });
                }

                // 4. GGMS Transactions
                try
                {
                    using var ggmsDb = GgmsDbContextFactory.Create();
                    var headBeneficiaries = await db.Beneficiaries
                        .Where(b => b.EmpId == Head.EmpId)
                        .Select(b => new { b.BeneficiaryId, b.CivilRegistryId })
                        .ToListAsync();

                    var benIds = headBeneficiaries.Where(b => !string.IsNullOrEmpty(b.BeneficiaryId)).Select(b => b.BeneficiaryId).ToList();
                    var civIds = headBeneficiaries.Where(b => !string.IsNullOrEmpty(b.CivilRegistryId)).Select(b => b.CivilRegistryId).ToList();

                    var ggmsTransactions = await ggmsDb.GgmsTransactions
                        .Where(t => (t.BeneficiaryId != null && benIds.Contains(t.BeneficiaryId)) ||
                                    (t.CivilRegistryId != null && civIds.Contains(t.CivilRegistryId)))
                        .ToListAsync();

                    foreach (var gt in ggmsTransactions)
                    {
                        transactionsList.Add(new ConsolidatedTransaction
                        {
                            Date = gt.TransactionDate.ToDateTime(TimeOnly.MinValue),
                            Type = $"GGMS - {gt.TransactionType}",
                            Description = $"{gt.ProjectName} ({gt.OfficeName})",
                            Amount = gt.Amount,
                            Status = gt.Status
                        });
                    }
                }
                catch { /* Ignore if GGMS offline */ }

                // Sort and display
                Transactions.Clear();
                foreach (var t in transactionsList.OrderByDescending(x => x.Date))
                {
                    Transactions.Add(t);
                }
            }
            catch (Exception ex)
            {
                App.ReportError("Failed to load profile details", ex);
            }
        }
    }
}
