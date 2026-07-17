using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("user_permissions")]
    public class UserPermission
    {
        [Key]
        [Column("permission_id")]
        public int PermissionId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("can_access_dashboard")]
        public bool CanAccessDashboard { get; set; } = true;

        [Column("can_access_employees")]
        public bool CanAccessEmployees { get; set; } = true;

        [Column("can_access_beneficiaries")]
        public bool CanAccessBeneficiaries { get; set; } = true;

        [Column("can_access_policies")]
        public bool CanAccessPolicies { get; set; } = true;

        [Column("can_access_claims")]
        public bool CanAccessClaims { get; set; } = true;

        [Column("can_access_premiums")]
        public bool CanAccessPremiums { get; set; } = true;

        [Column("can_access_benefits")]
        public bool CanAccessBenefits { get; set; } = true;

        [Column("can_access_documents")]
        public bool CanAccessDocuments { get; set; } = true;

        [Column("can_access_transactions")]
        public bool CanAccessTransactions { get; set; } = true;

        [Column("can_access_cedulas")]
        public bool CanAccessCedulas { get; set; } = true;

        [Column("can_access_reports")]
        public bool CanAccessReports { get; set; } = true;

        [Column("can_access_company_profile")]
        public bool CanAccessCompanyProfile { get; set; } = true;

        [Column("can_access_settings")]
        public bool CanAccessSettings { get; set; } = true;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [ForeignKey(nameof(UserId))]
        public SystemUser? User { get; set; }
    }
}
