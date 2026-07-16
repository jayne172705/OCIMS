using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using OCIMS.Models;

namespace OCIMS.Data
{
    public class DocumentRepository
    {
        // ── GET ALL DOCUMENTS ────────────────────────────────
        public List<Document> GetAll()
        {
            var list = new List<Document>();
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT d.document_id, d.emp_id,
                               CONCAT(e.first_name,' ',e.last_name) AS client_name,
                               e.employee_no AS client_id,
                               d.doc_type_id,
                               dt.type_name AS doc_type_name,
                               d.doc_title, d.file_name, d.file_path,
                               IFNULL(d.file_size,'') AS file_size,
                               IFNULL(d.remarks,'') AS remarks,
                               d.created_at
                        FROM documents d
                        JOIN employees     e  ON e.emp_id     = d.emp_id
                        JOIN document_types dt ON dt.doc_type_id = d.doc_type_id
                        WHERE d.is_active = 1
                        ORDER BY d.created_at DESC";

                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new Document
                            {
                                DocumentId = Convert.ToInt32(r["document_id"]),
                                EmpId = Convert.ToInt32(r["emp_id"]),
                                ClientName = r["client_name"].ToString(),
                                ClientId = r["client_id"].ToString(),
                                DocTypeId = Convert.ToInt32(r["doc_type_id"]),
                                DocTypeName = r["doc_type_name"].ToString(),
                                DocTitle = r["doc_title"].ToString(),
                                FileName = r["file_name"].ToString(),
                                FilePath = r["file_path"].ToString(),
                                FileSize = r["file_size"].ToString(),
                                Remarks = r["remarks"].ToString(),
                                DateUploaded = AppFormats.ToDisplayDate(Convert.ToDateTime(r["created_at"]))
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("DB Error: " + ex.Message, "eSureHi",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            return list;
        }

        // ── GET DOCUMENT TYPES ───────────────────────────────
        public List<DocumentType> GetDocumentTypes()
        {
            var list = new List<DocumentType>();
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT doc_type_id, type_name, IFNULL(description,'') AS description FROM document_types WHERE is_active=1 ORDER BY type_name";
                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new DocumentType
                            {
                                DocTypeId = Convert.ToInt32(r["doc_type_id"]),
                                TypeName = r["type_name"].ToString(),
                                Description = r["description"].ToString()
                            });
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        // ── SAVE ─────────────────────────────────────────────
        public bool Save(Document doc)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();

                    // Get emp_id from employee_no
                    int empId = GetEmpId(conn, doc.ClientId);
                    if (empId == 0)
                    {
                        System.Windows.MessageBox.Show(
                            "Client ID '" + doc.ClientId + "' was not found. Please check the Client ID and try again.",
                            "eSureHi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return false;
                    }

                    string sql = @"
                        INSERT INTO documents
                            (emp_id, doc_type_id, doc_title, file_name,
                             file_path, file_size, remarks, is_active)
                        VALUES
                            (@empid, @typeid, @title, @filename,
                             @filepath, @filesize, @remarks, 1)";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@empid", empId);
                        cmd.Parameters.AddWithValue("@typeid", doc.DocTypeId);
                        cmd.Parameters.AddWithValue("@title", doc.DocTitle);
                        cmd.Parameters.AddWithValue("@filename", doc.FileName ?? "");
                        cmd.Parameters.AddWithValue("@filepath", doc.FilePath ?? "");
                        cmd.Parameters.AddWithValue("@filesize", doc.FileSize ?? "");
                        cmd.Parameters.AddWithValue("@remarks", doc.Remarks ?? "");
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error saving: " + ex.Message, "eSureHi",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        // ── DELETE ───────────────────────────────────────────
        public bool Delete(int documentId)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "UPDATE documents SET is_active=0 WHERE document_id=@id";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", documentId);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error deleting: " + ex.Message, "eSureHi",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        private int GetEmpId(MySqlConnection conn, string employeeNo)
        {
            try
            {
                string sql = "SELECT emp_id FROM employees WHERE employee_no=@no LIMIT 1";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@no", employeeNo);
                    var result = cmd.ExecuteScalar();
                    if (result != null) return Convert.ToInt32(result);
                }
            }
            catch { }
            return 0;
        }
    }
}
