using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("ggms_allocation_cache")]
    public class GgmsAllocationCache
    {
        [Key]
        [Column("allocation_cache_id")]
        public long AllocationCacheId { get; set; }

        [Column("office_code")]
        public string OfficeCode { get; set; } = string.Empty;

        [Column("year")]
        public int Year { get; set; }

        [Column("allocated_amount")]
        public decimal AllocatedAmount { get; set; }

        [Column("spent_amount")]
        public decimal SpentAmount { get; set; }

        [Column("remaining_amount")]
        public decimal RemainingAmount { get; set; }

        [Column("cached_at")]
        public DateTime CachedAt { get; set; } = DateTime.Now;
    }
}
