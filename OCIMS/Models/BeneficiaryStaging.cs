using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("beneficiary_staging")]
    public class BeneficiaryStaging
    {
        [Key]
        [Column("staging_id")]
        public long StagingId { get; set; }

        [Column("residents_id")]
        public long? ResidentsId { get; set; }

        [Column("beneficiary_id")]
        public string BeneficiaryId { get; set; } = string.Empty;

        [Column("civilregistry_id")]
        public string? CivilRegistryId { get; set; }

        [Column("last_name")]
        public string? LastName { get; set; }

        [Column("first_name")]
        public string? FirstName { get; set; }

        [Column("middle_name")]
        public string? MiddleName { get; set; }

        [Column("full_name")]
        public string? FullName { get; set; }

        [Column("sex")]
        public string? Sex { get; set; }

        [Column("date_of_birth")]
        public string? DateOfBirth { get; set; }

        [Column("age")]
        public string? Age { get; set; }

        [Column("marital_status")]
        public string? MaritalStatus { get; set; }

        [Column("address")]
        public string? Address { get; set; }

        [Column("is_pwd")]
        public bool IsPwd { get; set; } = false;

        [Column("pwd_id_no")]
        public string? PwdIdNo { get; set; }

        [Column("is_senior")]
        public bool IsSenior { get; set; } = false;

        [Column("senior_id_no")]
        public string? SeniorIdNo { get; set; }

        [Column("disability_type")]
        public string? DisabilityType { get; set; }

        [Column("cause_of_disability")]
        public string? CauseOfDisability { get; set; }

        [Column("link_status")]
        public string LinkStatus { get; set; } = "Unlinked";

        [Column("linked_emp_id")]
        public int? LinkedEmpId { get; set; }

        [Column("linked_ben_id")]
        public int? LinkedBenId { get; set; }

        [Column("imported_at")]
        public DateTime ImportedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public string DisplayName =>
            !string.IsNullOrWhiteSpace(FullName)
                ? FullName
                : $"{LastName}, {FirstName} {MiddleName}".Trim();

        [NotMapped]
        public bool IsRegisteredMember { get; set; }

        [NotMapped]
        public string RegisteredRole { get; set; } = string.Empty;

        [NotMapped]
        public string RegisteredMemberName { get; set; } = string.Empty;

        [NotMapped]
        public string RegisteredHouseholdId { get; set; } = string.Empty;

        [NotMapped]
        public string RegisteredWorkflowStatus { get; set; } = string.Empty;

        [NotMapped]
        public bool HasDemographicProfile { get; set; }

        [NotMapped]
        public string DemographicFamilyId { get; set; } = string.Empty;

        [NotMapped]
        public string DemographicFamilyRole { get; set; } = string.Empty;

        [NotMapped]
        public string DemographicRelationshipToHead { get; set; } = string.Empty;

        [NotMapped]
        public bool IsDemographicHeadOfFamily { get; set; }

        [NotMapped]
        public string RegisteredRoleBadge =>
            IsRegisteredMember
                ? NormalizeRoleBadge(string.IsNullOrWhiteSpace(RegisteredRole) ? "REGISTERED" : RegisteredRole)
                : HasDemographicProfile && !string.IsNullOrWhiteSpace(DemographicFamilyRole)
                    ? NormalizeRoleBadge(DemographicFamilyRole)
                : "NOT REGISTERED";

        [NotMapped]
        public string ResidentSearchRoleBadge =>
            IsRegisteredMember
                ? RegisteredRoleBadge
                : HasDemographicProfile && !string.IsNullOrWhiteSpace(DemographicFamilyRole)
                    ? NormalizeRoleBadge(DemographicFamilyRole)
                    : "UNCLASSIFIED";

        [NotMapped]
        public string ResidentSearchHouseholdId =>
            !string.IsNullOrWhiteSpace(DemographicFamilyId)
                ? DemographicFamilyId
                : !string.IsNullOrWhiteSpace(BeneficiaryId)
                    ? BeneficiaryId
                    : ResidentsId?.ToString() ?? "-";

        [NotMapped]
        public string RegistrationDetailText =>
            IsRegisteredMember
                ? $"Registered as {(string.IsNullOrWhiteSpace(RegisteredRole) ? "member" : RegisteredRole)}"
                : HasDemographicProfile && !string.IsNullOrWhiteSpace(DemographicFamilyRole)
                    ? $"Demographic role: {DemographicFamilyRole}"
                : "Not registered as a member yet";

        private static string NormalizeRoleBadge(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return "UNCLASSIFIED";

            var value = role.Trim();
            if (value.Equals("Head of the Family", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("Head of Family", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("Family Head", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("Household Head", StringComparison.OrdinalIgnoreCase))
            {
                return "FAMILY HEAD";
            }

            return value.ToUpperInvariant();
        }
    }
}
