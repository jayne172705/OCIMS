using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("document_types")]
    public class DocumentType
    {
        [Key]
        [Column("doc_type_id")]
        public int DocTypeId { get; set; }

        [Column("type_name")]
        public string TypeName { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
