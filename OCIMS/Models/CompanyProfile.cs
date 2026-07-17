using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("company_profile")]
    public class CompanyProfile
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = "Municipality of Sulop";

        [Column("address")]
        public string? Address { get; set; }

        [Column("contact_number")]
        public string? ContactNumber { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("logo_path")]
        public string? LogoPath { get; set; }
    }
}
