using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Models;

namespace eSureHi.Services
{
    public static class DemoDataSeederService
    {
        private sealed class SeedPolicyDefinition
        {
            public string Code { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
            public string Type { get; init; } = string.Empty;
            public string Provider { get; init; } = string.Empty;
            public decimal CoverageAmount { get; init; }
            public decimal EmployeeShare { get; init; }
            public decimal EmployerShare { get; init; }
            public string Description { get; init; } = string.Empty;
            public string Terms { get; init; } = string.Empty;
            public IReadOnlyList<(string BenefitType, decimal MaxBenefit, string Notes)> Benefits { get; init; } =
                Array.Empty<(string BenefitType, decimal MaxBenefit, string Notes)>();
        }

        public static async Task SeedCoreInsuranceDemoDataAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                var departments = await EnsureDepartmentsAsync(db);
                await CleanupObsoleteSeedPoliciesAsync(db);
                var policies = await EnsurePoliciesAsync(db);
                var employees = await EnsureEmployeesAsync(db, departments, minimumEmployees: 20);

                await EnsureUserAccountsAsync(db, employees);
                await EnsureEmployeePoliciesAsync(db, employees, policies);
                await EnsurePremiumsAsync(db);
                await EnsureCompanyProfileAsync(db);
                await EnsureDocumentTypesAsync(db);
                await EnsureBeneficiariesAsync(db, employees, minimumBeneficiaries: 10);
                await EnsureClaimsAsync(db);
                await EnsureDocumentsAsync(db);
                await EnsureDocumentTransactionsAsync(db);
                await EnsureCedulasAsync(db, employees);
                await EnsureContributionsAsync(db, employees);
                await EnsureTrainingsAsync(db, employees);
                await EnsureBeneficiaryStagingAsync(db, employees);
                await EnsureNotificationsAsync(db);
            }
            catch
            {
                // Demo seeding is non-critical and should never block startup.
            }
        }

        public static async Task PurgeDemoDataAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();

                // 1. Identify Demo Entities
                var demoEmployees = await db.Employees
                    .Where(e => e.EmployeeNo.StartsWith("SULOP-2026-"))
                    .ToListAsync();
                var demoEmpIds = demoEmployees.Select(e => e.EmpId).ToList();

                // 2. Delete Dependent Records (Child to Parent)
                
                // Notifications
                try {
                    var demoNotifs = await db.Notifications
                        .Where(n => n.Title.Contains("Demo") || n.Title.Contains("Welcome") || n.Message!.Contains("simulation"))
                        .ToListAsync();
                    if (demoNotifs.Any()) db.Notifications.RemoveRange(demoNotifs);
                } catch { /* Table may not exist */ }

                // Document Transactions
                try {
                    var demoTxns = await db.DocumentTransactions
                        .Where(t => t.TransactionNo.StartsWith("TXN-DEMO-"))
                        .ToListAsync();
                    if (demoTxns.Any()) db.DocumentTransactions.RemoveRange(demoTxns);
                } catch { /* Table may not exist */ }

                // Documents
                try {
                    var demoDocs = await db.Documents
                        .Where(d => demoEmpIds.Contains(d.EmpId) || d.Remarks!.Contains("Seeded"))
                        .ToListAsync();
                    if (demoDocs.Any()) db.Documents.RemoveRange(demoDocs);
                } catch { /* Table may not exist */ }

                // Trainings
                try {
                    var demoTrainings = await db.Trainings
                        .Where(t => demoEmpIds.Contains(t.EmployeeId))
                        .ToListAsync();
                    if (demoTrainings.Any()) db.Trainings.RemoveRange(demoTrainings);
                } catch { /* Table may not exist */ }

                // Contributions
                try {
                    var demoContributions = await db.Contributions
                        .Where(c => demoEmpIds.Contains(c.EmployeeId))
                        .ToListAsync();
                    if (demoContributions.Any()) db.Contributions.RemoveRange(demoContributions);
                } catch { /* Table may not exist */ }

                // Cedulas
                try {
                    var demoCedulas = await db.Cedulas
                        .Where(c => demoEmpIds.Contains(c.EmployeeId) || c.CedulaNo.StartsWith("CED-BEN-"))
                        .ToListAsync();
                    if (demoCedulas.Any()) db.Cedulas.RemoveRange(demoCedulas);
                } catch { /* Table may not exist */ }

                // Claims
                try {
                    var demoClaims = await db.Claims
                        .Where(c => demoEmpIds.Contains(c.EmpId) || c.ClaimNo.StartsWith("CLM-DEMO-"))
                        .ToListAsync();
                    if (demoClaims.Any()) db.Claims.RemoveRange(demoClaims);
                } catch { /* Table may not exist */ }

                // Benefits & Premiums (linked to EmployeePolicies)
                try {
                    var demoEpIds = await db.EmployeePolicies
                        .Where(ep => demoEmpIds.Contains(ep.EmpId))
                        .Select(ep => ep.EpId)
                        .ToListAsync();

                    var demoBenefits = await db.Benefits
                        .Where(b => demoEpIds.Contains(b.EpId))
                        .ToListAsync();
                    if (demoBenefits.Any()) db.Benefits.RemoveRange(demoBenefits);

                    var demoPremiums = await db.Premiums
                        .Where(p => demoEpIds.Contains(p.EpId))
                        .ToListAsync();
                    if (demoPremiums.Any()) db.Premiums.RemoveRange(demoPremiums);
                } catch { /* Table may not exist */ }

                // Employee Policies
                try {
                    var demoEps = await db.EmployeePolicies
                        .Where(ep => demoEmpIds.Contains(ep.EmpId))
                        .ToListAsync();
                    if (demoEps.Any()) db.EmployeePolicies.RemoveRange(demoEps);
                } catch { /* Table may not exist */ }

                // Beneficiaries
                try {
                    var demoBens = await db.Beneficiaries
                        .Where(b => demoEmpIds.Contains(b.EmpId))
                        .ToListAsync();
                    if (demoBens.Any()) db.Beneficiaries.RemoveRange(demoBens);
                } catch { /* Table may not exist */ }

                // System Users (Employee accounts)
                try {
                    var demoUsers = await db.SystemUsers
                        .Where(u => u.Username == "jericho" || (u.EmpId.HasValue && demoEmpIds.Contains(u.EmpId.Value)))
                        .ToListAsync();
                    if (demoUsers.Any()) db.SystemUsers.RemoveRange(demoUsers);
                } catch { /* Table may not exist */ }

                // Employees
                try {
                    if (demoEmployees.Any()) db.Employees.RemoveRange(demoEmployees);
                } catch { /* Table may not exist */ }

                // Beneficiary Staging
                try {
                    var demoStaging = await db.BeneficiaryStaging
                        .Where(b => b.BeneficiaryId.StartsWith("CRS-DEMO-"))
                        .ToListAsync();
                    if (demoStaging.Any()) db.BeneficiaryStaging.RemoveRange(demoStaging);
                } catch { /* Table may not exist */ }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Demo data purge failed: {ex.Message}", ex);
            }
        }

        private static async Task<List<Department>> EnsureDepartmentsAsync(eSureHiDbContext db)
        {
            var now = DateTime.Now;
            var definitions = new[]
            {
                ("Mayor's Office", "MO", "Executive and administrative supervision."),
                ("Municipal Health Office", "MHO", "Health and social insurance coordination."),
                ("Municipal Treasury Office", "MTO", "Premium remittance and collections."),
                ("Human Resource Office", "HR", "Personnel records and employee services."),
                ("Municipal Social Welfare Office", "MSWDO", "Social benefits and community assistance."),
                ("Assessor's Office", "ASSR", "Property and assessment administration.")
            };

            foreach (var (name, code, description) in definitions)
            {
                var existing = await db.Departments.FirstOrDefaultAsync(d => d.DeptCode == code);
                if (existing is not null)
                {
                    if (!existing.IsActive)
                    {
                        existing.IsActive = true;
                        existing.UpdatedAt = now;
                    }
                    continue;
                }

                db.Departments.Add(new Department
                {
                    DeptName = name,
                    DeptCode = code,
                    Description = description,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await db.SaveChangesAsync();
            return await db.Departments
                .Where(d => d.IsActive)
                .OrderBy(d => d.DeptName)
                .ToListAsync();
        }

        private static async Task<List<InsurancePolicy>> EnsurePoliciesAsync(eSureHiDbContext db)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var nextYear = today.AddYears(1);
            var renewal = nextYear.AddMonths(-1);
            var now = DateTime.Now;

            var definitions = GetPolicyDefinitions();

            foreach (var def in definitions)
            {
                var policy = await db.InsurancePolicies
                    .FirstOrDefaultAsync(p => p.PolicyCode == def.Code);

                if (policy is null)
                {
                    policy = new InsurancePolicy
                    {
                        PolicyCode = def.Code,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    db.InsurancePolicies.Add(policy);
                }

                policy.PolicyName = def.Name;
                policy.PolicyType = def.Type;
                policy.ProviderName = def.Provider;
                policy.ProviderContact = "(082) 123-4567";
                policy.EffectiveDate = today;
                policy.ExpiryDate = nextYear;
                policy.RenewalDate = renewal;
                policy.CoverageAmount = def.CoverageAmount;
                policy.Description = def.Description;
                policy.TermsConditions = def.Terms;
                policy.PolicyStatus = "Active";
                policy.UpdatedAt = now;
            }

            await db.SaveChangesAsync();
            return await db.InsurancePolicies
                .Where(p => definitions.Select(d => d.Code).Contains(p.PolicyCode))
                .OrderBy(p => p.PolicyName)
                .ToListAsync();
        }

        private static async Task CleanupObsoleteSeedPoliciesAsync(eSureHiDbContext db)
        {
            var validCodes = GetPolicyDefinitions()
                .Select(x => x.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var obsoleteCodes = new[]
            {
                "POL-DENTAL-001",
                "POL-VISION-001",
                "POL-RETIRE-001"
            };

            var obsoletePolicies = await db.InsurancePolicies
                .Where(p => obsoleteCodes.Contains(p.PolicyCode) || !validCodes.Contains(p.PolicyCode) &&
                            (p.PolicyCode.StartsWith("POL-") ||
                             (p.ProviderName ?? string.Empty).Contains("Municipal") ||
                             (p.ProviderName ?? string.Empty).Contains("PhilCare")))
                .ToListAsync();

            if (!obsoletePolicies.Any())
                return;

            var obsoletePolicyIds = obsoletePolicies.Select(p => p.PolicyId).ToList();
            var obsoleteAssignments = await db.EmployeePolicies
                .Where(ep => obsoletePolicyIds.Contains(ep.PolicyId))
                .ToListAsync();

            if (obsoleteAssignments.Any())
            {
                var obsoleteEpIds = obsoleteAssignments.Select(ep => ep.EpId).ToList();

                var obsoleteBenefits = await db.Benefits
                    .Where(b => obsoleteEpIds.Contains(b.EpId))
                    .ToListAsync();
                var obsoletePremiums = await db.Premiums
                    .Where(p => obsoleteEpIds.Contains(p.EpId))
                    .ToListAsync();

                if (obsoleteBenefits.Any())
                    db.Benefits.RemoveRange(obsoleteBenefits);
                if (obsoletePremiums.Any())
                    db.Premiums.RemoveRange(obsoletePremiums);

                db.EmployeePolicies.RemoveRange(obsoleteAssignments);
            }

            db.InsurancePolicies.RemoveRange(obsoletePolicies);
            await db.SaveChangesAsync();
        }

        private static async Task<List<Employee>> EnsureEmployeesAsync(
            eSureHiDbContext db,
            List<Department> departments,
            int minimumEmployees)
        {
            var existingEmployees = await db.Employees
                .OrderBy(e => e.EmpId)
                .ToListAsync();

            var targetToAdd = Math.Max(0, minimumEmployees - existingEmployees.Count);
            if (targetToAdd == 0)
                return existingEmployees;

            var firstNames = new[]
            {
                "Ana", "Mark", "Liza", "Rogelio", "Carla", "Jessa", "Noel", "Shane", "Paolo", "Grace",
                "Kevin", "Mira", "Ramil", "Donna", "Eric", "Jonalyn", "Patrick", "Rose", "Arnold", "Mica"
            };
            var lastNames = new[]
            {
                "Bautista", "Fernandez", "Lopez", "Garcia", "Ramos", "Dela Cruz", "Santos", "Mendoza", "Torres", "Aquino",
                "Valdez", "Navarro", "Castro", "Reyes", "Flores", "Villanueva", "Domingo", "Marquez", "Salazar", "Lazaro"
            };
            var positions = new[]
            {
                "Administrative Aide", "Clerk", "HR Staff", "Treasury Staff", "Records Officer",
                "Program Coordinator", "Nurse Assistant", "Social Welfare Aide", "Cash Clerk", "Planning Assistant"
            };
            var barangays = new[]
            {
                "Poblacion", "Litos", "Rizal", "Talus", "Carre", "Little Baguio", "Tuban", "Palili", "Harada Butai", "Roxas"
            };
            var employmentTypes = new[] { "Regular", "Job Order", "Casual" };
            var civilStatuses = new[] { "Single", "Married", "Widowed" };
            var genders = new[] { "Male", "Female" };

            var startIndex = existingEmployees.Count;
            var now = DateTime.Now;

            for (int i = 0; i < targetToAdd; i++)
            {
                var overallIndex = startIndex + i;
                var dept = departments[overallIndex % departments.Count];
                var firstName = firstNames[overallIndex % firstNames.Length];
                var lastName = lastNames[overallIndex % lastNames.Length];
                var employeeNo = $"SULOP-{DateTime.Today.Year}-{overallIndex + 1:0000}";

                var exists = await db.Employees.AnyAsync(e => e.EmployeeNo == employeeNo);
                if (exists)
                    continue;

                var dateHired = DateOnly.FromDateTime(DateTime.Today.AddDays(-(overallIndex + 1) * 35));
                var dateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-24 - (overallIndex % 18)).AddDays(overallIndex * -13));

                db.Employees.Add(new Employee
                {
                    EmployeeNo = employeeNo,
                    FirstName = firstName,
                    MiddleName = "M",
                    LastName = lastName,
                    DateOfBirth = dateOfBirth,
                    Gender = genders[overallIndex % genders.Length],
                    CivilStatus = civilStatuses[overallIndex % civilStatuses.Length],
                    Nationality = "Filipino",
                    Email = $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}{overallIndex + 1}@sulop.gov.ph",
                    PhoneMobile = $"0917{(overallIndex + 1111111):0000000}",
                    PhoneOffice = $"082-{2000 + overallIndex}",
                    AddressLine1 = $"Purok {overallIndex % 7 + 1}",
                    AddressLine2 = "Municipal Housing Compound",
                    City = "Sulop",
                    Barangay = barangays[overallIndex % barangays.Length],
                    Province = "Davao del Sur",
                    ZipCode = "8009",
                    DeptId = dept.DeptId,
                    PositionTitle = positions[overallIndex % positions.Length],
                    EmploymentType = employmentTypes[overallIndex % employmentTypes.Length],
                    DateHired = dateHired,
                    EmploymentStatus = "Active",
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await db.SaveChangesAsync();
            return await db.Employees
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }

        private static async Task EnsureUserAccountsAsync(eSureHiDbContext db, List<Employee> employees)
        {
            var now = DateTime.Now;
            foreach (var employee in employees.Take(20))
            {
                var username = employee.EmployeeNo.ToLowerInvariant().Replace("-", "");
                var existing = await db.SystemUsers.FirstOrDefaultAsync(u => u.EmpId == employee.EmpId);
                if (existing is not null)
                    continue;

                db.SystemUsers.Add(new SystemUser
                {
                    EmpId = employee.EmpId,
                    Username = username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
                    Role = "Employee",
                    IsActive = true,
                    CreatedAt = now
                });
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureEmployeePoliciesAsync(
            eSureHiDbContext db,
            List<Employee> employees,
            List<InsurancePolicy> policies)
        {
            var definitions = GetPolicyDefinitions().ToDictionary(x => x.Code);
            var now = DateTime.Now;
            var activeEmployees = employees.Where(e => e.EmploymentStatus == "Active").Take(20).ToList();

            foreach (var employee in activeEmployees)
            {
                var assignedPolicies = GetPolicyCodesForEmployee(employee).ToList();

                foreach (var policyCode in assignedPolicies)
                {
                    var policy = policies.First(p => p.PolicyCode == policyCode);
                    var existing = await db.EmployeePolicies
                        .FirstOrDefaultAsync(ep => ep.EmpId == employee.EmpId &&
                                                   ep.PolicyId == policy.PolicyId &&
                                                   ep.AssignmentStatus == "Active");

                    if (existing is null)
                    {
                        existing = new EmployeePolicy
                        {
                            EmpId = employee.EmpId,
                            PolicyId = policy.PolicyId,
                            CreatedAt = now
                        };
                        db.EmployeePolicies.Add(existing);
                    }

                    var def = definitions[policyCode];
                    existing.CoverageLimit = policy.CoverageAmount;
                    existing.EmployeeShare = def.EmployeeShare;
                    existing.EmployerShare = def.EmployerShare;
                    existing.StartDate = policy.EffectiveDate;
                    existing.EndDate = policy.ExpiryDate;
                    existing.AssignmentStatus = "Active";
                    existing.Remarks = $"{policy.PolicyName} with benefits: {string.Join(", ", def.Benefits.Select(b => b.BenefitType))}";
                    existing.UpdatedAt = now;
                }
            }

            await db.SaveChangesAsync();

            var seededAssignments = await db.EmployeePolicies
                .Include(ep => ep.Policy)
                .Where(ep => activeEmployees.Select(e => e.EmpId).Contains(ep.EmpId) &&
                             ep.AssignmentStatus == "Active")
                .ToListAsync();

            foreach (var assignment in seededAssignments)
            {
                if (assignment.Policy is null)
                    continue;

                var def = definitions[assignment.Policy.PolicyCode];
                foreach (var benefitDef in def.Benefits)
                {
                    var existingBenefit = await db.Benefits.FirstOrDefaultAsync(b =>
                        b.EpId == assignment.EpId &&
                        b.BenefitType == benefitDef.BenefitType &&
                        b.YearPeriod == DateTime.Today.Year);

                    if (existingBenefit is not null)
                        continue;

                    db.Benefits.Add(new Benefit
                    {
                        EpId = assignment.EpId,
                        BenefitType = benefitDef.BenefitType,
                        YearPeriod = DateTime.Today.Year,
                        MaxBenefit = benefitDef.MaxBenefit,
                        UsedBenefit = 0,
                        LastUsedDate = null,
                        Notes = benefitDef.Notes,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsurePremiumsAsync(eSureHiDbContext db)
        {
            var now = DateTime.Now;
            var months = new[]
            {
                DateOnly.FromDateTime(new DateTime(DateTime.Today.Year, Math.Max(1, DateTime.Today.Month - 1), 1)),
                DateOnly.FromDateTime(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)),
                DateOnly.FromDateTime(new DateTime(DateTime.Today.Year, Math.Min(12, DateTime.Today.Month == 12 ? 12 : DateTime.Today.Month + 1), 1))
            }.Distinct().ToList();

            var activeAssignments = await db.EmployeePolicies
                .Where(ep => ep.AssignmentStatus == "Active")
                .ToListAsync();

            foreach (var assignment in activeAssignments)
            {
                foreach (var billingMonth in months)
                {
                    var exists = await db.Premiums.AnyAsync(p => p.EpId == assignment.EpId &&
                                                                 p.BillingMonth == billingMonth);
                    if (exists)
                        continue;

                    var totalShare = assignment.EmployeeShare + assignment.EmployerShare;
                    var dueDate = new DateOnly(billingMonth.Year, billingMonth.Month,
                        Math.Min(28, DateTime.DaysInMonth(billingMonth.Year, billingMonth.Month)));
                    var isPastOrCurrent = billingMonth <= DateOnly.FromDateTime(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
                    var amountPaid = isPastOrCurrent ? totalShare : 0;

                    db.Premiums.Add(new Premium
                    {
                        EpId = assignment.EpId,
                        BillingMonth = billingMonth,
                        EmployeeAmount = assignment.EmployeeShare,
                        EmployerAmount = assignment.EmployerShare,
                        TotalAmount = totalShare,
                        DueDate = dueDate,
                        PaidDate = isPastOrCurrent ? dueDate : null,
                        PaymentMode = "Payroll Deduction",
                        PaymentStatus = isPastOrCurrent ? "Paid" : "Unpaid",
                        AmountPaid = amountPaid,
                        Balance = isPastOrCurrent ? 0 : totalShare,
                        ReferenceNo = isPastOrCurrent ? $"PMT-{assignment.EpId}-{billingMonth:yyyyMM}" : null,
                        LateFee = 0,
                        Remarks = isPastOrCurrent ? "Seeded paid premium." : "Seeded upcoming premium.",
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureCompanyProfileAsync(eSureHiDbContext db)
        {
            var profile = await db.CompanyProfiles.FirstOrDefaultAsync();
            if (profile is null)
            {
                db.CompanyProfiles.Add(new CompanyProfile
                {
                    Name = "Municipality of Sulop",
                    Address = "Kiblagon, Sulop, Davao del Sur",
                    ContactNumber = "(082) 123-4567",
                    Email = "hrmo@sulop.gov.ph"
                });
            }
            else
            {
                profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? "Municipality of Sulop" : profile.Name;
                profile.Address ??= "Kiblagon, Sulop, Davao del Sur";
                profile.ContactNumber ??= "(082) 123-4567";
                profile.Email ??= "hrmo@sulop.gov.ph";
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureDocumentTypesAsync(eSureHiDbContext db)
        {
            var now = DateTime.Now;
            var definitions = new[]
            {
                ("Personal Records", "Birth certificates, IDs, and personal records."),
                ("Employment Contract", "Signed contracts and appointment papers."),
                ("Insurance Policy", "Insurance coverage documents."),
                ("Medical Records", "Health clearances and medical history."),
                ("Beneficiary Documents", "Forms related to beneficiaries."),
                ("Educational Background", "Diplomas, transcripts, and certificates."),
                ("Cedula", "Community tax certificate records."),
                ("Other Documents", "Miscellaneous files.")
            };

            foreach (var (name, description) in definitions)
            {
                var existing = await db.DocumentTypes.FirstOrDefaultAsync(t => t.TypeName == name);
                if (existing is null)
                {
                    db.DocumentTypes.Add(new DocumentType
                    {
                        TypeName = name,
                        Description = description,
                        IsActive = true,
                        CreatedAt = now
                    });
                }
                else
                {
                    existing.Description ??= description;
                    existing.IsActive = true;
                }
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureBeneficiariesAsync(
            eSureHiDbContext db,
            List<Employee> employees,
            int minimumBeneficiaries)
        {
            var currentCount = await db.Beneficiaries.CountAsync();
            if (currentCount >= minimumBeneficiaries)
                return;

            var now = DateTime.Now;
            var firstNames = new[] { "Maria", "Jose", "Angela", "Miguel", "Sofia", "Daniel", "Andrea", "Carlo", "Rica", "Luis" };
            var lastNames = new[] { "Dela Cruz", "Santos", "Reyes", "Garcia", "Mendoza", "Torres", "Aquino", "Flores", "Castro", "Navarro" };
            var relationships = new[] { "Spouse", "Child", "Parent", "Sibling" };
            var activeEmployees = employees.Where(e => e.EmploymentStatus == "Active").Take(20).ToList();
            var toAdd = minimumBeneficiaries - currentCount;

            for (int i = 0; i < toAdd && i < activeEmployees.Count * 2; i++)
            {
                var employee = activeEmployees[i % activeEmployees.Count];
                var firstName = firstNames[i % firstNames.Length];
                var lastName = lastNames[i % lastNames.Length];

                var exists = await db.Beneficiaries.AnyAsync(b =>
                    b.EmpId == employee.EmpId &&
                    b.FirstName == firstName &&
                    b.LastName == lastName);

                if (exists)
                    continue;

                db.Beneficiaries.Add(new Beneficiary
                {
                    EmpId = employee.EmpId,
                    FirstName = firstName,
                    LastName = lastName,
                    Relationship = relationships[i % relationships.Length],
                    DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-18 - i).AddDays(-i * 17)),
                    Gender = i % 2 == 0 ? "Female" : "Male",
                    IsPrimary = !await db.Beneficiaries.AnyAsync(b => b.EmpId == employee.EmpId && b.IsPrimary),
                    IsActive = true,
                    CreatedAt = now,
                    RecipientsInsurance = employee.EmploymentType,
                    CedulaNo = $"CED-BEN-{DateTime.Today.Year}-{i + 1:0000}",
                    Received = i % 3 != 0,
                    Contribution = 100 + (i * 25)
                });
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureClaimsAsync(eSureHiDbContext db)
        {
            var existingClaimNos = await db.Claims.Select(c => c.ClaimNo).ToListAsync();
            var assignments = await db.EmployeePolicies
                .Include(ep => ep.Policy)
                .Where(ep => ep.AssignmentStatus == "Active")
                .OrderBy(ep => ep.EpId)
                .Take(10)
                .ToListAsync();

            var adminId = await db.SystemUsers
                .Where(u => u.Role != "Employee")
                .Select(u => (int?)u.UserId)
                .FirstOrDefaultAsync();

            var now = DateTime.Now;
            var statuses = new[] { "Draft", "Submitted", "Under Review", "Approved", "Released", "Rejected" };

            for (int i = 0; i < assignments.Count; i++)
            {
                var claimNo = $"CLM-DEMO-{DateTime.Today.Year}-{i + 1:0000}";
                if (existingClaimNos.Contains(claimNo))
                    continue;

                var assignment = assignments[i];
                var beneficiaryId = await db.Beneficiaries
                    .Where(b => b.EmpId == assignment.EmpId)
                    .Select(b => (int?)b.BenId)
                    .FirstOrDefaultAsync();
                var status = statuses[i % statuses.Length];
                var amount = 3000m + (i * 850m);

                db.Claims.Add(new Claim
                {
                    ClaimNo = claimNo,
                    EmpId = assignment.EmpId,
                    PolicyId = assignment.PolicyId,
                    BenId = beneficiaryId,
                    ClaimType = assignment.Policy?.PolicyType ?? "Health",
                    ClaimDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-20 + i)),
                    IncidentDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-24 + i)),
                    IncidentDescription = $"Demo claim for {assignment.Policy?.PolicyName ?? "insurance policy"}.",
                    HospitalClinic = i % 2 == 0 ? "Sulop Municipal Health Office" : "Davao del Sur Provincial Hospital",
                    AttendingPhysician = i % 2 == 0 ? "Dr. A. Santos" : "Dr. M. Reyes",
                    AmountClaimed = amount,
                    AmountApproved = status is "Approved" or "Released" ? amount * 0.8m : 0,
                    AmountReleased = status == "Released" ? amount * 0.8m : 0,
                    ClaimStatus = status,
                    SubmittedDate = status == "Draft" ? null : now.AddDays(-10 + i),
                    ReviewedDate = status is "Under Review" or "Approved" or "Released" or "Rejected" ? now.AddDays(-7 + i) : null,
                    ApprovedDate = status is "Approved" or "Released" ? now.AddDays(-4 + i) : null,
                    ReleasedDate = status == "Released" ? now.AddDays(-1) : null,
                    ReviewedBy = status == "Draft" ? null : adminId,
                    ApprovedBy = status is "Approved" or "Released" ? adminId : null,
                    RejectionReason = status == "Rejected" ? "Incomplete supporting documents." : null,
                    Remarks = "Seeded demo claim.",
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureDocumentsAsync(eSureHiDbContext db)
        {
            var docTypes = await db.DocumentTypes.OrderBy(t => t.DocTypeId).ToListAsync();
            var employees = await db.Employees.OrderBy(e => e.EmpId).Take(10).ToListAsync();
            var adminId = await db.SystemUsers.Where(u => u.Role != "Employee").Select(u => (int?)u.UserId).FirstOrDefaultAsync();
            var now = DateTime.Now;

            for (int i = 0; i < employees.Count && docTypes.Any(); i++)
            {
                var type = docTypes[i % docTypes.Count];
                var title = $"{type.TypeName} - {employees[i].EmployeeNo}";
                var exists = await db.Documents.AnyAsync(d => d.EmpId == employees[i].EmpId && d.DocTitle == title);
                if (exists)
                    continue;

                db.Documents.Add(new Document
                {
                    EmpId = employees[i].EmpId,
                    DocTypeId = type.DocTypeId,
                    DocTitle = title,
                    FileName = $"{employees[i].EmployeeNo}_{type.TypeName.Replace(" ", "_")}.pdf",
                    FilePath = $@"C:\eSureHi\DemoDocuments\{employees[i].EmployeeNo}_{type.TypeName.Replace(" ", "_")}.pdf",
                    FileSize = $"{120 + (i * 18)} KB",
                    Remarks = "Seeded document placeholder for simulation.",
                    UploadedBy = adminId,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureDocumentTransactionsAsync(eSureHiDbContext db)
        {
            var now = DateTime.Now;
            var senders = await EnsureSendersAsync(db);
            var receivers = await EnsureReceiversAsync(db);
            var documents = await db.Documents.OrderBy(d => d.DocumentId).Take(10).ToListAsync();
            var adminId = await db.SystemUsers.Where(u => u.Role != "Employee").Select(u => (int?)u.UserId).FirstOrDefaultAsync();
            var statuses = new[] { "Pending", "In Progress", "Completed", "Returned" };

            for (int i = 0; i < 10; i++)
            {
                var transactionNo = $"TXN-DEMO-{DateTime.Today.Year}-{i + 1:0000}";
                if (await db.DocumentTransactions.AnyAsync(t => t.TransactionNo == transactionNo))
                    continue;

                db.DocumentTransactions.Add(new DocumentTransaction
                {
                    TransactionNo = transactionNo,
                    DocumentId = documents.ElementAtOrDefault(i % Math.Max(1, documents.Count))?.DocumentId,
                    TransactionType = i % 2 == 0 ? "Incoming" : "Outgoing",
                    SenderId = senders[i % senders.Count].SenderId,
                    ReceiverId = receivers[i % receivers.Count].ReceiverId,
                    Subject = $"Demo routing transaction {i + 1}",
                    Description = "Seeded document transaction for simulation.",
                    TransactionDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-i)),
                    DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5 + i)),
                    Priority = i % 3 == 0 ? "High" : "Normal",
                    Status = statuses[i % statuses.Length],
                    Remarks = "Seeded transaction.",
                    ProcessedBy = adminId,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await db.SaveChangesAsync();
        }

        private static async Task<List<Sender>> EnsureSendersAsync(eSureHiDbContext db)
        {
            var now = DateTime.Now;
            var names = new[] { "HRMO", "Municipal Budget Office", "Municipal Treasury Office", "GGMS Coordination Desk" };
            foreach (var name in names)
            {
                if (await db.Senders.AnyAsync(s => s.SenderName == name))
                    continue;
                db.Senders.Add(new Sender
                {
                    SenderName = name,
                    SenderType = name.Contains("Davao") ? "External" : "Internal",
                    Email = $"{name.ToLowerInvariant().Replace(" ", ".")}@example.com",
                    Phone = "(082) 123-4567",
                    Address = "Sulop, Davao del Sur",
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            await db.SaveChangesAsync();
            return await db.Senders.Where(s => names.Contains(s.SenderName)).ToListAsync();
        }

        private static async Task<List<Receiver>> EnsureReceiversAsync(eSureHiDbContext db)
        {
            var now = DateTime.Now;
            var names = new[] { "Records Section", "Claims Desk", "Accounting Unit", "Office of the Mayor", "Employee Services" };
            foreach (var name in names)
            {
                if (await db.Receivers.AnyAsync(r => r.ReceiverName == name))
                    continue;
                db.Receivers.Add(new Receiver
                {
                    ReceiverName = name,
                    ReceiverType = "Internal",
                    Email = $"{name.ToLowerInvariant().Replace(" ", ".")}@sulop.gov.ph",
                    Phone = "(082) 123-4567",
                    Department = name,
                    Address = "Municipal Hall, Sulop",
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            await db.SaveChangesAsync();
            return await db.Receivers.Where(r => names.Contains(r.ReceiverName)).ToListAsync();
        }

        private static async Task EnsureCedulasAsync(eSureHiDbContext db, List<Employee> employees)
        {
            var now = DateTime.Now;
            foreach (var employee in employees.Take(10))
            {
                if (await db.Cedulas.AnyAsync(c => c.EmployeeId == employee.EmpId))
                    continue;

                db.Cedulas.Add(new Cedula
                {
                    EmployeeId = employee.EmpId,
                    CedulaNo = $"CED-{DateTime.Today.Year}-{employee.EmpId:0000}",
                    IssueDate = DateTime.Today.AddDays(-(employee.EmpId % 60)),
                    PlaceIssued = "Municipality of Sulop",
                    AmountPaid = 50 + (employee.EmpId % 5) * 10,
                    Remarks = "Seeded cedula record.",
                    CreatedAt = now
                });
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureContributionsAsync(eSureHiDbContext db, List<Employee> employees)
        {
            var now = DateTime.Now;
            foreach (var employee in employees.Take(10))
            {
                for (int i = 0; i < 2; i++)
                {
                    var date = DateOnly.FromDateTime(DateTime.Today.AddMonths(-i));
                    if (await db.Contributions.AnyAsync(c => c.EmployeeId == employee.EmpId && c.Date == date))
                        continue;

                    db.Contributions.Add(new Contribution
                    {
                        EmployeeId = employee.EmpId,
                        Date = date,
                        Amount = 250 + (employee.EmpId % 4) * 50,
                        Remarks = "Seeded monthly contribution.",
                        OrNumber = $"OR-{DateTime.Today.Year}{date.Month:00}-{employee.EmpId:0000}-{i + 1}",
                        CreatedAt = now
                    });
                }
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureTrainingsAsync(eSureHiDbContext db, List<Employee> employees)
        {
            var titles = new[]
            {
                "Insurance Claims Orientation",
                "Data Privacy and Records Handling",
                "Employee Benefits Briefing",
                "Document Routing Workshop"
            };

            foreach (var employee in employees.Take(10))
            {
                var title = titles[employee.EmpId % titles.Length];
                if (await db.Trainings.AnyAsync(t => t.EmployeeId == employee.EmpId && t.Title == title))
                    continue;

                db.Trainings.Add(new Training
                {
                    EmployeeId = employee.EmpId,
                    Title = title,
                    Description = "Seeded training record for employee profile simulation.",
                    DateAttended = DateOnly.FromDateTime(DateTime.Today.AddDays(-(employee.EmpId * 3))),
                    DurationHours = 4 + (employee.EmpId % 3) * 2,
                    InstructorOrVenue = "Sulop Municipal Hall"
                });
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureBeneficiaryStagingAsync(eSureHiDbContext db, List<Employee> employees)
        {
            var now = DateTime.Now;
            var firstNames = new[] { "Marites", "Jun", "Evelyn", "Ronaldo", "Clarissa", "Benedicto", "Lorna", "Emmanuel", "Jocelyn", "Arnel" };
            var lastNames = new[] { "Apostol", "Balili", "Cabang", "Dacumos", "Eclarin", "Fuentes", "Gonzales", "Hernandez", "Ibañez", "Javier" };

            for (int i = 0; i < 10; i++)
            {
                var beneficiaryId = $"CRS-DEMO-{DateTime.Today.Year}-{i + 1:0000}";
                if (await db.BeneficiaryStaging.AnyAsync(b => b.BeneficiaryId == beneficiaryId))
                    continue;

                var employee = employees.ElementAtOrDefault(i % employees.Count);
                db.BeneficiaryStaging.Add(new BeneficiaryStaging
                {
                    ResidentsId = 900000 + i,
                    BeneficiaryId = beneficiaryId,
                    CivilRegistryId = $"CR-{DateTime.Today.Year}-{i + 1:0000}",
                    LastName = lastNames[i],
                    FirstName = firstNames[i],
                    MiddleName = "M",
                    FullName = $"{firstNames[i]} M {lastNames[i]}",
                    Sex = i % 2 == 0 ? "Female" : "Male",
                    DateOfBirth = DateTime.Today.AddYears(-25 - i).ToString("yyyy-MM-dd"),
                    Age = (25 + i).ToString(),
                    MaritalStatus = i % 2 == 0 ? "Single" : "Married",
                    Address = "Sulop, Davao del Sur",
                    IsPwd = i == 3,
                    PwdIdNo = i == 3 ? "PWD-DEMO-0003" : null,
                    IsSenior = i == 8,
                    SeniorIdNo = i == 8 ? "SC-DEMO-0008" : null,
                    LinkStatus = i < 5 ? "Linked" : "Unlinked",
                    LinkedEmpId = i < 5 ? employee?.EmpId : null,
                    ImportedAt = now
                });
            }

            await db.SaveChangesAsync();
        }

        private static async Task EnsureNotificationsAsync(eSureHiDbContext db)
        {
            var users = await db.SystemUsers.OrderBy(u => u.UserId).Take(10).ToListAsync();
            var now = DateTime.Now;
            foreach (var user in users)
            {
                var title = "Demo data ready";
                if (await db.Notifications.AnyAsync(n => n.RecipientId == user.UserId && n.Title == title))
                    continue;

                db.Notifications.Add(new Notification
                {
                    RecipientId = user.UserId,
                    NotifType = "System",
                    Title = title,
                    Message = "Sulop simulation seed data is available across dashboard pages.",
                    IsRead = false,
                    CreatedAt = now.AddMinutes(-user.UserId)
                });
            }

            await db.SaveChangesAsync();
        }

        private static IEnumerable<string> GetPolicyCodesForEmployee(Employee employee)
        {
            yield return employee.EmploymentType switch
            {
                "Job Order" => "POL-JOBORDER-001",
                "Casual" => "POL-CASUAL-001",
                _ => "POL-REGULAR-001"
            };
        }

        private static List<SeedPolicyDefinition> GetPolicyDefinitions()
        {
            return new List<SeedPolicyDefinition>
            {
                CreateEmploymentTrackPolicy("POL-JOBORDER-001", "Job Order", 250000m),
                CreateEmploymentTrackPolicy("POL-CASUAL-001", "Casual", 500000m),
                CreateEmploymentTrackPolicy("POL-REGULAR-001", "Regular", 1000000m)
            };
        }

        private static SeedPolicyDefinition CreateEmploymentTrackPolicy(
            string code,
            string employmentType,
            decimal budgetAllocation)
        {
            return new SeedPolicyDefinition
            {
                Code = code,
                Name = $"{employmentType} Employee Budget Track",
                Type = employmentType,
                Provider = "LGU Sulop HRMO / Budget Office",
                CoverageAmount = budgetAllocation,
                EmployeeShare = 0m,
                EmployerShare = 0m,
                Description = $"Budget-controlled track for {employmentType.ToLowerInvariant()} employee applicants and assignments.",
                Terms = "When this track budget is depleted, request additional funding through GGMS before further releases.",
                Benefits = new[]
                {
                    ("Employment Support", budgetAllocation, $"{employmentType} allocation monitored against available GGMS funds.")
                }
            };
        }
    }
}
