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

        [Column("cedula_no")]
        public string? CedulaNo { get; set; }

        [Column("imported_at")]
        public DateTime ImportedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public string DisplayName =>
            !string.IsNullOrWhiteSpace(FullName)
                ? FullName
                : $"{LastName}, {FirstName} {MiddleName}".Trim();

        [NotMapped]
        public bool IsRegisteredMember
        {
            get => LinkStatus == "Linked";
            set { }
        }

        [NotMapped]
        public string RegisteredRole { get; set; } = string.Empty;

        [NotMapped]
        public string RegisteredMemberName { get; set; } = string.Empty;

        [NotMapped]
        public string RegisteredHouseholdId { get; set; } = string.Empty;

        [NotMapped]
        public string RegisteredWorkflowStatus { get; set; } = string.Empty;

        [NotMapped]
        public string? HouseholdId { get; set; }

        [NotMapped]
        public string? FamilyId { get; set; }

        [NotMapped]
        public string? FamilyRole { get; set; }

        [NotMapped]
        public string? RelationshipToHead { get; set; }

        [NotMapped]
        public bool IsHouseholdHead { get; set; }

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
            var label = MapPositionToLabel(role);
            return string.IsNullOrWhiteSpace(label) ? "UNCLASSIFIED" : label;
        }

        // Maps a raw CRS demographic_characteristics.position value to a clean
        // display label. Handles underscores, inconsistent casing, and truncated
        // values such as "brother_in...". Returns null when the value is empty or
        // whitespace so callers can fall back to "UNCLASSIFIED".
        public static string? MapPositionToLabel(string? rawPosition)
        {
            if (string.IsNullOrWhiteSpace(rawPosition))
                return null;

            // Normalize separators (underscores/dots) and casing to a single
            // lowercase, single-spaced key for matching.
            var key = rawPosition.Replace('_', ' ').Replace('.', ' ').Trim().ToLowerInvariant();
            while (key.Contains("  "))
                key = key.Replace("  ", " ");

            if (key.Length == 0)
                return null;

            // Head of family — several spellings collapse to one badge.
            if (key is "head" or "head of family" or "head of the family"
                or "family head" or "household head" or "hh head" or "householdhead")
                return "Head of Family";

            // In-law relations, including truncated forms like "brother in..." →
            // "brother in". Detect the base relation before the "in" marker.
            if (key.Contains("in law") || key.Contains("inlaw") ||
                key.EndsWith(" in") || key.Contains(" in "))
            {
                var basePart = key.Split(new[] { " in" }, StringSplitOptions.None)[0].Trim();
                var baseLabel = basePart switch
                {
                    "brother" => "Brother",
                    "sister" => "Sister",
                    "father" => "Father",
                    "mother" => "Mother",
                    "son" => "Son",
                    "daughter" => "Daughter",
                    _ => TitleCase(basePart)
                };
                if (!string.IsNullOrWhiteSpace(baseLabel))
                    return $"{baseLabel}-in-law";
            }

            return key switch
            {
                "spouse" or "wife" or "husband" => "Spouse",
                "son" => "Son",
                "daughter" => "Daughter",
                "child" or "children" => "Child",
                "father" => "Father",
                "mother" => "Mother",
                "parent" => "Parent",
                "brother" => "Brother",
                "sister" => "Sister",
                "sibling" => "Sibling",
                "grandson" => "Grandson",
                "granddaughter" => "Granddaughter",
                "grandchild" => "Grandchild",
                "grandfather" or "grandpa" or "lolo" => "Grandfather",
                "grandmother" or "grandma" or "lola" => "Grandmother",
                "grandparent" => "Grandparent",
                "uncle" => "Uncle",
                "aunt" or "auntie" => "Aunt",
                "nephew" => "Nephew",
                "niece" => "Niece",
                "cousin" => "Cousin",
                "stepson" => "Stepson",
                "stepdaughter" => "Stepdaughter",
                "stepchild" => "Stepchild",
                "stepfather" => "Stepfather",
                "stepmother" => "Stepmother",
                "ward" => "Ward",
                "boarder" => "Boarder",
                "househelp" or "house help" or "helper" or "maid" or "kasambahay" => "House Helper",
                "relative" or "other relative" => "Relative",
                "non relative" or "nonrelative" or "non-relative" => "Non-relative",
                "other" or "others" => "Other",
                // Unknown but present → clean Title Case rather than "UNCLASSIFIED".
                _ => TitleCase(key)
            };
        }

        private static string TitleCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < words.Length; i++)
            {
                var w = words[i];
                words[i] = char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w.Substring(1) : string.Empty);
            }
            return string.Join(' ', words);
        }
    }
}
