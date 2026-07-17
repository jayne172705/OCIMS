using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSureHi.Models
{
    [Table("claim_documents")]
    public class ClaimDocument
    {
        [Key]
        [Column("doc_id")]
        public int DocId { get; set; }

        [Column("claim_id")]
        public int ClaimId { get; set; }

        [Column("doc_type")]
        public string DocType { get; set; } = string.Empty;

        [Column("file_name")]
        public string FileName { get; set; } = string.Empty;

        [Column("file_path")]
        public string FilePath { get; set; } = string.Empty;

        [Column("file_size_kb")]
        public int? FileSizeKb { get; set; }

        [Column("uploaded_by")]
        public int? UploadedBy { get; set; }

        [Column("uploaded_at")]
        public DateTime UploadedAt { get; set; }

        // Navigation
        [ForeignKey("ClaimId")]
        public Claim? Claim { get; set; }
    }
}
