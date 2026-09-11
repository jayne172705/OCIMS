using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Models;
using eSureHi.ViewModels.Admin;

namespace eSureHi.Services
{
    public class HouseholdSnapshot
    {
        public string? Address { get; init; }
        public string? FamilyId { get; init; }
        public bool IsHouseholdHead { get; init; }
        public List<FamilyMemberDisplay> Members { get; init; } = new();
    }

    public static class HouseholdLookupService
    {
        // Resolves family/household composition and each member's registration status,
        // extracted from DistributionIdCardViewModel.LoadAsync so both the Distribution
        // and Claim-filing flows share the exact same lookup rules.
        public static async Task<HouseholdSnapshot> GetHouseholdSnapshotAsync(eSureHiDbContext db, Beneficiary beneficiary)
        {
            var snapshot = new HouseholdSnapshot();
            string? familyId = null;
            string? address = null;
            bool isHouseholdHead = false;

            // The CRS cache belongs to the device-local SQLite database. When the
            // main data source is Online IMS, use that local cache for household
            // details while retaining the supplied context for IMS beneficiaries.
            eSureHiDbContext? ownedCacheDb = null;
            var cacheDb = db;
            if (!db.Database.IsSqlite())
            {
                ownedCacheDb = eSureHiDbContextFactory.CreateLocal();
                cacheDb = ownedCacheDb;
            }

            try
            {

                if (!string.IsNullOrWhiteSpace(beneficiary.CivilRegistryId))
                {
                    var cacheRow = await cacheDb.CrsBeneficiaryCache
                        .FirstOrDefaultAsync(c => c.CivilRegistryId == beneficiary.CivilRegistryId);
                    if (cacheRow != null)
                    {
                        familyId = cacheRow.FamilyId;
                        address = string.IsNullOrWhiteSpace(cacheRow.Address) ? null : cacheRow.Address;
                        isHouseholdHead = cacheRow.IsHouseholdHead;
                    }
                }

                var members = new List<FamilyMemberDisplay>();
                if (!string.IsNullOrWhiteSpace(familyId))
                {
                    var familyMembers = await cacheDb.CrsBeneficiaryCache
                        .Where(c => c.FamilyId == familyId)
                        .ToListAsync();

                    var civilRegistryIds = familyMembers
                        .Where(m => !string.IsNullOrWhiteSpace(m.CivilRegistryId))
                        .Select(m => m.CivilRegistryId)
                        .ToList();

                    var activeBeneficiaries = await db.Beneficiaries
                        .Where(b => b.IsActive && civilRegistryIds.Contains(b.CivilRegistryId))
                        .Select(b => new { b.CivilRegistryId, b.IsPrimary })
                        .ToListAsync();

                    foreach (var member in familyMembers
                                 .OrderBy(m => m.IsHouseholdHead ? 0 : 1)
                                 .ThenBy(m => m.FullName))
                    {
                        var status = "Not Registered";
                        var bg = "#F1F5F9";
                        var fg = "#64748B";

                        if (!string.IsNullOrWhiteSpace(member.CivilRegistryId))
                        {
                            var ben = activeBeneficiaries.FirstOrDefault(b => b.CivilRegistryId == member.CivilRegistryId);
                            if (ben != null)
                            {
                                if (ben.IsPrimary) { status = "Registered Member"; bg = "#DCFCE7"; fg = "#166534"; }
                                else { status = "Dependent"; bg = "#DBEAFE"; fg = "#1E40AF"; }
                            }
                        }

                        members.Add(new FamilyMemberDisplay
                        {
                            FullName = member.FullName ?? "Unknown",
                            FamilyRole = string.IsNullOrWhiteSpace(member.FamilyRole) ? "MEMBER" : member.FamilyRole!.ToUpper(),
                            RegistrationStatus = status,
                            RegistrationStatusBackground = bg,
                            RegistrationStatusForeground = fg
                        });
                    }
                }

                return new HouseholdSnapshot
                {
                    Address = address,
                    FamilyId = familyId,
                    IsHouseholdHead = isHouseholdHead,
                    Members = members
                };
            }
            finally
            {
                if (ownedCacheDb is not null)
                    await ownedCacheDb.DisposeAsync();
            }
        }
    }
}
