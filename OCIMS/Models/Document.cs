namespace OCIMS.Models
{
    public class Document
    {
        public int DocumentId { get; set; }
        public int EmpId { get; set; }
        public string ClientName { get; set; }
        public string ClientId { get; set; }
        public int DocTypeId { get; set; }
        public string DocTypeName { get; set; }
        public string DocTitle { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string FileSize { get; set; }
        public string Remarks { get; set; }
        public string DateUploaded { get; set; }

        public string Icon
        {
            get
            {
                if (FileName == null) return "📄";
                string ext = System.IO.Path.GetExtension(FileName).ToUpper();
                if (ext == ".PDF") return "📕";
                if (ext == ".DOC" || ext == ".DOCX") return "📘";
                if (ext == ".XLS" || ext == ".XLSX") return "📗";
                if (ext == ".PNG" || ext == ".JPG" || ext == ".JPEG") return "🖼";
                return "📄";
            }
        }
    }

    public class DocumentType
    {
        public int DocTypeId { get; set; }
        public string TypeName { get; set; }
        public string Description { get; set; }
    }
}
