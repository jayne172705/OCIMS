using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using OCIMS.Models;

namespace OCIMS.Data
{
    public class TransactionRepository
    {
        // ── GET ALL TRANSACTIONS ─────────────────────────────
        public List<DocumentTransaction> GetAll()
        {
            var list = new List<DocumentTransaction>();
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT t.transaction_id, t.transaction_no,
                               t.transaction_type,
                               IFNULL(s.sender_name,'—')   AS sender_name,
                               IFNULL(r.receiver_name,'—') AS receiver_name,
                               t.subject,
                               IFNULL(t.description,'')    AS description,
                               t.transaction_date,
                               IFNULL(t.due_date,'')        AS due_date,
                               t.priority, t.status,
                               IFNULL(t.remarks,'')         AS remarks,
                               IFNULL(d.doc_title,'—')     AS doc_title
                        FROM document_transactions t
                        LEFT JOIN senders   s ON s.sender_id   = t.sender_id
                        LEFT JOIN receivers r ON r.receiver_id = t.receiver_id
                        LEFT JOIN documents d ON d.document_id = t.document_id
                        ORDER BY t.transaction_date DESC, t.created_at DESC";

                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var tx = new DocumentTransaction
                            {
                                TransactionId = Convert.ToInt32(reader["transaction_id"]),
                                TransactionNo = reader["transaction_no"].ToString(),
                                TransactionType = reader["transaction_type"].ToString(),
                                SenderName = reader["sender_name"].ToString(),
                                ReceiverName = reader["receiver_name"].ToString(),
                                Subject = reader["subject"].ToString(),
                                Description = reader["description"].ToString(),
                                Priority = reader["priority"].ToString(),
                                Status = reader["status"].ToString(),
                                Remarks = reader["remarks"].ToString(),
                                DocumentTitle = reader["doc_title"].ToString()
                            };

                            var txDate = reader["transaction_date"];
                            if (txDate != DBNull.Value)
                                tx.TransactionDate = Convert.ToDateTime(txDate).ToString("MMM dd, yyyy");

                            var dueDate = reader["due_date"];
                            if (dueDate != DBNull.Value && !string.IsNullOrEmpty(dueDate.ToString()))
                                tx.DueDate = Convert.ToDateTime(dueDate).ToString("MMM dd, yyyy");
                            else
                                tx.DueDate = "—";

                            list.Add(tx);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("DB Error: " + ex.Message, "OCIMS",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            return list;
        }

        // ── GET SENDERS ──────────────────────────────────────
        public List<Sender> GetSenders()
        {
            var list = new List<Sender>();
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT sender_id, sender_name, sender_type, IFNULL(email,'') AS email, IFNULL(phone,'') AS phone FROM senders WHERE is_active=1 ORDER BY sender_name";
                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(new Sender
                            {
                                SenderId = Convert.ToInt32(r["sender_id"]),
                                SenderName = r["sender_name"].ToString(),
                                SenderType = r["sender_type"].ToString(),
                                Email = r["email"].ToString(),
                                Phone = r["phone"].ToString()
                            });
                    }
                }
            }
            catch { }
            return list;
        }

        // ── GET RECEIVERS ────────────────────────────────────
        public List<Receiver> GetReceivers()
        {
            var list = new List<Receiver>();
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT receiver_id, receiver_name, receiver_type, IFNULL(department,'') AS department, IFNULL(email,'') AS email, IFNULL(phone,'') AS phone FROM receivers WHERE is_active=1 ORDER BY receiver_name";
                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            list.Add(new Receiver
                            {
                                ReceiverId = Convert.ToInt32(r["receiver_id"]),
                                ReceiverName = r["receiver_name"].ToString(),
                                ReceiverType = r["receiver_type"].ToString(),
                                Department = r["department"].ToString(),
                                Email = r["email"].ToString(),
                                Phone = r["phone"].ToString()
                            });
                    }
                }
            }
            catch { }
            return list;
        }

        // ── SAVE TRANSACTION ─────────────────────────────────
        public bool Save(DocumentTransaction tx, int senderId, int receiverId)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        INSERT INTO document_transactions
                            (transaction_no, transaction_type, sender_id, receiver_id,
                             subject, description, transaction_date, due_date,
                             priority, status, remarks)
                        VALUES
                            (@txno, @type, @sender, @receiver,
                             @subject, @desc, @txdate, @duedate,
                             @priority, 'Pending', @remarks)";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@txno", tx.TransactionNo);
                        cmd.Parameters.AddWithValue("@type", tx.TransactionType);
                        cmd.Parameters.AddWithValue("@sender", senderId > 0 ? (object)senderId : DBNull.Value);
                        cmd.Parameters.AddWithValue("@receiver", receiverId > 0 ? (object)receiverId : DBNull.Value);
                        cmd.Parameters.AddWithValue("@subject", tx.Subject);
                        cmd.Parameters.AddWithValue("@desc", tx.Description ?? "");
                        cmd.Parameters.AddWithValue("@txdate", DateTime.Today);
                        cmd.Parameters.AddWithValue("@duedate", string.IsNullOrEmpty(tx.DueDate) || tx.DueDate == "—"
                                                                    ? (object)DBNull.Value
                                                                    : DateTime.Parse(tx.DueDate));
                        cmd.Parameters.AddWithValue("@priority", tx.Priority ?? "Normal");
                        cmd.Parameters.AddWithValue("@remarks", tx.Remarks ?? "");
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error saving: " + ex.Message, "OCIMS",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        // ── UPDATE STATUS ────────────────────────────────────
        public bool UpdateStatus(int transactionId, string status)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "UPDATE document_transactions SET status=@status WHERE transaction_id=@id";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@status", status);
                        cmd.Parameters.AddWithValue("@id", transactionId);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch { return false; }
        }

        // ── DELETE ───────────────────────────────────────────
        public bool Delete(int transactionId)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "DELETE FROM document_transactions WHERE transaction_id=@id";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", transactionId);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch { return false; }
        }

        // ── CHECK DUPLICATE TX NO ────────────────────────────
        public bool TxNoExists(string txNo)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT COUNT(*) FROM document_transactions WHERE transaction_no=@no";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@no", txNo);
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch { return false; }
        }
    }
}
