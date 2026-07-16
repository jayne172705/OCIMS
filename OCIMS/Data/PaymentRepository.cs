using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using OCIMS.Models;

namespace OCIMS.Data
{
    public class PaymentRepository
    {
        public List<Payment> GetAll()
        {
            var list = new List<Payment>();
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT payment_id, payment_no, client_name, amount,
                               payment_date, IFNULL(payment_mode,'') AS payment_mode,
                               payment_status, IFNULL(notes,'') AS notes,
                               IFNULL(emp_id,0) AS emp_id
                        FROM payments
                        ORDER BY payment_date DESC, payment_id DESC";

                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new Payment
                            {
                                PaymentId = Convert.ToInt32(r["payment_id"]),
                                PaymentNo = r["payment_no"].ToString(),
                                ClientName = r["client_name"].ToString(),
                                Amount = Convert.ToDecimal(r["amount"]),
                                PaymentDate = AppFormats.ToDisplayDate(Convert.ToDateTime(r["payment_date"])),
                                PaymentMode = r["payment_mode"].ToString(),
                                PaymentStatus = r["payment_status"].ToString(),
                                Notes = r["notes"].ToString(),
                                EmpId = Convert.ToInt32(r["emp_id"])
                            });
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

        public bool Save(Payment p)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        INSERT INTO payments
                            (payment_no, emp_id, client_name, amount,
                             payment_date, payment_mode, payment_status, notes)
                        VALUES
                            (@no, @empid, @client, @amount,
                             @date, @mode, @status, @notes)";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        DateTime payDate;
                        if (!AppFormats.TryParseDisplayDate(p.PaymentDate, out payDate))
                            payDate = DateTime.Today;

                        cmd.Parameters.AddWithValue("@no", p.PaymentNo);
                        cmd.Parameters.AddWithValue("@empid", p.EmpId > 0 ? (object)p.EmpId : DBNull.Value);
                        cmd.Parameters.AddWithValue("@client", p.ClientName);
                        cmd.Parameters.AddWithValue("@amount", p.Amount);
                        cmd.Parameters.AddWithValue("@date", payDate);
                        cmd.Parameters.AddWithValue("@mode", p.PaymentMode ?? "");
                        cmd.Parameters.AddWithValue("@status", p.PaymentStatus ?? "Paid");
                        cmd.Parameters.AddWithValue("@notes", p.Notes ?? "");
                        cmd.ExecuteNonQuery();
                        p.PaymentId = (int)cmd.LastInsertedId;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error saving payment: " + ex.Message, "OCIMS",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        public bool Delete(int paymentId)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("DELETE FROM payments WHERE payment_id=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", paymentId);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error deleting payment: " + ex.Message, "OCIMS",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        public bool PaymentNoExists(string paymentNo)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM payments WHERE payment_no=@no", conn))
                    {
                        cmd.Parameters.AddWithValue("@no", paymentNo);
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch { return false; }
        }
    }
}
