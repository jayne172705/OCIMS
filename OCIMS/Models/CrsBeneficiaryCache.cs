using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("crs_beneficiary_cache")]
    public class CrsBeneficiaryCache
    {
        [Key]
        [Column("beneficiary_cache_id")]
        public long BeneficiaryCacheId { get; set; }

        [Column("beneficiary_id")]
        public string BeneficiaryId { get; set; } = string.Empty;

        [Column("residents_id")]
        public long? ResidentsId { get; set; }

        [Column("civilregistry_id")]
        public string? CivilRegistryId { get; set; }

        [Column("full_name")]
        public string? FullName { get; set; }

        [Column("first_name")]
        public string? FirstName { get; set; }

        [Column("last_name")]
        public string? LastName { get; set; }

        [Column("middle_name")]
        public string? MiddleName { get; set; }

        [Column("sex")]
        public string? Sex { get; set; }

        [Column("age")]
        public string? Age { get; set; }

        [Column("address")]
        public string? Address { get; set; }

        [Column("date_of_birth")]
        public string? DateOfBirth { get; set; }

        [Column("marital_status")]
        public string? MaritalStatus { get; set; }

        [Column("is_pwd")]
        public bool IsPwd { get; set; }

        [Column("is_senior")]
        public bool IsSenior { get; set; }

        [Column("family_id")]
        public string? FamilyId { get; set; }

        [Column("household_id")]
        public string? HouseholdId { get; set; }

        [Column("family_role")]
        public string? FamilyRole { get; set; }

        [Column("relationship_to_head")]
        public string? RelationshipToHead { get; set; }

        [Column("is_household_head")]
        public bool IsHouseholdHead { get; set; }

        [Column("cached_at")]
        public DateTime CachedAt { get; set; } = DateTime.Now;
    }
}
