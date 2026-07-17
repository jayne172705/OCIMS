using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("documents")]
    public class Document
    {
        [Key]
        [Column("document_id")]
        public int DocumentId { get; set; }

        [Column("emp_id")]
        public int EmpId { get; set; }

        [Column("doc_type_id")]
        public int DocTypeId { get; set; }

        [Column("doc_title")]
        public string DocTitle { get; set; } = string.Empty;

        [Column("file_name")]
        public string FileName { get; set; } = string.Empty;

        [Column("file_path")]
        public string FilePath { get; set; } = string.Empty;

        [Column("file_size")]
        public string? FileSize { get; set; }

        [Column("remarks")]
        public string? Remarks { get; set; }

        [Column("uploaded_by")]
        public int? UploadedBy { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        // Navigation
        [ForeignKey("EmpId")]
        public Employee? Employee { get; set; }

        [ForeignKey("DocTypeId")]
        public DocumentType? DocumentType { get; set; }
    }
}
