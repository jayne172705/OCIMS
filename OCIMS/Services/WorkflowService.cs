using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Models;

namespace eSureHi.Services
{
    public static class WorkflowService
    {
        public static async Task<(bool Success, string Message)> UpdateBeneficiaryStatusAsync(
            int beneficiaryId, 
            string status, 
            string? remarks = null)
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var ben = await db.Beneficiaries.FindAsync(beneficiaryId);
                if (ben == null) return (false, "Beneficiary not found.");

                var oldStatus = ben.WorkflowStatus;
                ben.WorkflowStatus = status;
                ben.StatusRemarks = remarks;
                await db.SaveChangesAsync();

                await AuditService.LogUpdate("beneficiaries", beneficiaryId, 
                    $"Status changed from {oldStatus} to {status}. Remarks: {remarks}");

                await RecordTransactionAsync(
                    $"BEN-{ben.BenId:0000}",
                    "Beneficiary Validation",
                    $"Beneficiary: {ben.FullName}",
                    status,
                    remarks);

                return (true, "Status updated successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Error updating beneficiary status: {ex.Message}");
            }
        }

        public static async Task<(bool Success, string Message)> UpdateClaimStatusAsync(
            int claimId, 
            string status, 
            string? remarks = null,
            decimal? amountApproved = null,
            decimal? amountReleased = null,
            string? sourceOfFunds = null)
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var claim = await db.Claims
                    .Include(c => c.Employee)
                    .Include(c => c.Beneficiary)
                    .FirstOrDefaultAsync(c => c.ClaimId == claimId);
                if (claim == null) return (false, "Claim not found.");

                var oldStatus = claim.ClaimStatus;
                claim.ClaimStatus = status;
                claim.Remarks = remarks;
                claim.UpdatedAt = DateTime.Now;
                
                if (status == WorkflowStatuses.Approved || status == "Partially Approved")
                {
                    claim.ApprovedDate = DateTime.Now;
                    claim.ApprovedBy = AuthService.Instance.CurrentUser?.UserId;
                    if (amountApproved.HasValue) claim.AmountApproved = amountApproved.Value;
                }
                else if (status == WorkflowStatuses.Released)
                {
                    claim.ReleasedDate = DateTime.Now;
                    if (amountReleased.HasValue) claim.AmountReleased = amountReleased.Value;
                    if (sourceOfFunds != null) claim.SourceOfFunds = sourceOfFunds;

                    var localFundCoveredRelease = false;
                    if (claim.AmountReleased > 0 && !string.IsNullOrWhiteSpace(claim.SourceOfFunds))
                    {
                        var fund = await db.SourceFunds.FirstOrDefaultAsync(f => f.FundName == claim.SourceOfFunds);
                        if (fund != null && fund.RemainingAmount >= claim.AmountReleased)
                        {
                            fund.UsedAmount += claim.AmountReleased;
                            fund.UpdatedAt = DateTime.Now;
                            localFundCoveredRelease = true;
                        }
                    }

                    if (!localFundCoveredRelease)
                        return (false, $"Local {claim.SourceOfFunds} fund is missing or has insufficient remaining balance.");
                        
                    // ── Write to GGMS ──────────────────────────────────────────────
                    (bool ggmsSuccess, string ggmsMsg) = await GgmsService.RecordClaimReleaseAsync(
                        claimId: claim.ClaimId,
                        beneficiaryIdentity: claim.Beneficiary?.BeneficiaryId ?? claim.Employee?.EmployeeNo,
                        civilRegistryId: claim.Beneficiary?.CivilRegistryId,
                        amountReleased: claim.AmountReleased,
                        claimType: claim.ClaimType ?? string.Empty,
                        firstName: claim.Beneficiary?.FirstName ?? claim.Employee?.FirstName ?? string.Empty,
                        middleName: claim.Employee?.MiddleName,
                        lastName: claim.Beneficiary?.LastName ?? claim.Employee?.LastName ?? string.Empty,
                        recipientName: claim.Beneficiary?.FullName ?? claim.Employee?.FullName ?? string.Empty,
                        claimNo: claim.ClaimNo ?? string.Empty,
                        purpose: $"Insurance Claim - {claim.ClaimType}",
                        sourceOfFunds: claim.SourceOfFunds
                    );

                    if (!ggmsSuccess)
                    {
                        return (false, $"GGMS Recording Failed: {ggmsMsg}");
                    }
                }

                await db.SaveChangesAsync();

                await AuditService.LogUpdate("claims", claimId, 
                    $"Status changed from {oldStatus} to {status}. Remarks: {remarks}");

                if (status == WorkflowStatuses.Approved ||
                    status == "Partially Approved" ||
                    status == WorkflowStatuses.Rejected ||
                    status == WorkflowStatuses.Released)
                {
                    var claimant = claim.Beneficiary?.FullName ?? claim.Employee?.FullName ?? $"Employee ID: {claim.EmpId}";
                    var amount = status == WorkflowStatuses.Released
                        ? claim.AmountReleased
                        : claim.AmountApproved;
                    var details = amount > 0
                        ? $" - Amount: {amount:N2}"
                        : string.Empty;

                    await RecordTransactionAsync(
                        claim.ClaimNo ?? string.Empty,
                        "Beneficiary Insurance Claim",
                        $"{claimant} - {status}{details}",
                        status,
                        remarks);
                }

                return (true, "Status updated successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Error updating claim status: {ex.Message}");
            }
        }

        public static async Task<(bool Success, string Message)> UpdatePolicyStatusAsync(
            int epId, 
            string status, 
            string? remarks = null)
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var policy = await db.EmployeePolicies.FindAsync(epId);
                if (policy == null) return (false, "Employee policy assignment not found.");

                var oldStatus = policy.AssignmentStatus;
                policy.AssignmentStatus = status;
                policy.StatusRemarks = remarks;
                policy.UpdatedAt = DateTime.Now;
                await db.SaveChangesAsync();

                await AuditService.LogUpdate("employee_policies", epId, 
                    $"Status changed from {oldStatus} to {status}. Remarks: {remarks}");

                await RecordTransactionAsync(
                    $"POL-ASN-{policy.EpId:0000}",
                    "Policy Assignment Approval",
                    $"Policy ID: {policy.PolicyId} for Emp ID: {policy.EmpId}",
                    status,
                    remarks);

                return (true, "Status updated successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Error updating policy status: {ex.Message}");
            }
        }

        public static async Task<(bool Success, string Message)> UpdateCedulaStatusAsync(
            int cedulaId, 
            string status, 
            string? remarks = null)
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var cedula = await db.Cedulas.FindAsync(cedulaId);
                if (cedula == null) return (false, "Cedula record not found.");

                var oldStatus = cedula.WorkflowStatus;
                cedula.WorkflowStatus = status;
                cedula.StatusRemarks = remarks;
                await db.SaveChangesAsync();

                await AuditService.LogUpdate("cedulas", cedulaId, 
                    $"Status changed from {oldStatus} to {status}. Remarks: {remarks}");

                await RecordTransactionAsync(
                    cedula.CedulaNo,
                    "Cedula Verification",
                    $"Cedula for Emp ID: {cedula.EmployeeId}",
                    status,
                    remarks);

                return (true, "Status updated successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Error updating cedula status: {ex.Message}");
            }
        }

        public static async Task RecordTransactionAsync(
            string refNo,
            string type,
            string subject,
            string status,
            string? remarks)
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                db.DocumentTransactions.Add(new DocumentTransaction
                {
                    TransactionNo = $"WF-{DateTime.Now:yyyyMMdd}-{refNo}",
                    TransactionType = type,
                    Subject = subject,
                    Status = status,
                    Remarks = remarks,
                    ProcessedBy = AuthService.Instance.CurrentUser?.UserId,
                    TransactionDate = DateOnly.FromDateTime(DateTime.Today),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
                await db.SaveChangesAsync();
            }
            catch { /* Log failure should not stop workflow */ }
        }
    }
}
