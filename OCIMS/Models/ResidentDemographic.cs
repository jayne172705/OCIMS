using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("resident_demographics")]
    public class ResidentDemographic
    {
        [Key]
        [Column("resident_demographic_id")]
        public long ResidentDemographicId { get; set; }

        [Column("residents_id")]
        public long? ResidentsId { get; set; }

        [Column("beneficiary_id")]
        [MaxLength(80)]
        public string? BeneficiaryId { get; set; }

        [Column("civilregistry_id")]
        [MaxLength(80)]
        public string? CivilRegistryId { get; set; }

        [Column("family_id")]
        [MaxLength(80)]
        public string? FamilyId { get; set; }

        [Column("household_id")]
        [MaxLength(80)]
        public string? HouseholdId { get; set; }

        [Column("family_role")]
        [MaxLength(80)]
        public string? FamilyRole { get; set; }

        [Column("relationship_to_head")]
        [MaxLength(80)]
        public string? RelationshipToHead { get; set; }

        [Column("is_household_head")]
        public bool IsHouseholdHead { get; set; }

        [Column("source")]
        [MaxLength(80)]
        public string Source { get; set; } = "Manual";

        [Column("remarks")]
        [MaxLength(500)]
        public string? Remarks { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
