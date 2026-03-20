using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using OCIMS.Models;

namespace OCIMS.Data
{
    public class ClaimRepository
    {
        public List<Claim> GetAll()
        {
            var list = new List<Claim>();
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT c.claim_id, c.claim_no,
                               CONCAT(e.first_name,' ',e.last_name) AS client_name,
                               c.claim_type, c.claim_date,
                               c.amount_claimed, c.claim_status,
                               IFNULL(c.incident_description,'') AS description,
                               c.emp_id
                        FROM claims c
                        JOIN employees e ON e.emp_id = c.emp_id
                        ORDER BY c.claim_date DESC";

                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var c = new Claim
                            {
                                ClaimId = Convert.ToInt32(r["claim_id"]),
                                ClaimNo = r["claim_no"].ToString(),
                                ClientName = r["client_name"].ToString(),
                                ClaimType = r["claim_type"].ToString(),
                                ClaimDate = Convert.ToDateTime(r["claim_date"]).ToString("MMM dd, yyyy"),
                                Amount = Convert.ToDecimal(r["amount_claimed"]),
                                ClaimStatus = r["claim_status"].ToString(),
                                Description = r["description"].ToString(),
                                EmpId = Convert.ToInt32(r["emp_id"])
                            };
                            list.Add(c);
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

        public bool Save(Claim c)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        INSERT INTO claims
                            (claim_no, emp_id, policy_id, claim_type,
                             claim_date, amount_claimed, claim_status,
                             incident_description, submitted_date)
                        VALUES
                            (@claimno, @empid, 1, @type,
                             @date, @amount, 'Pending',
                             @desc, NOW())";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@claimno", c.ClaimNo);
                        cmd.Parameters.AddWithValue("@empid", c.EmpId);
                        cmd.Parameters.AddWithValue("@type", c.ClaimType);
                        cmd.Parameters.AddWithValue("@date", DateTime.Today);
                        cmd.Parameters.AddWithValue("@amount", c.Amount);
                        cmd.Parameters.AddWithValue("@desc", c.Description ?? "");
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

        public bool UpdateStatus(int claimId, string status)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "UPDATE claims SET claim_status=@status WHERE claim_id=@id";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@status", status);
                        cmd.Parameters.AddWithValue("@id", claimId);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error: " + ex.Message, "OCIMS",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        public bool Delete(int claimId)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "DELETE FROM claims WHERE claim_id=@id";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", claimId);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error: " + ex.Message, "OCIMS",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        public bool ClaimNoExists(string claimNo)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT COUNT(*) FROM claims WHERE claim_no=@no";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@no", claimNo);
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch { return false; }
        }
    }
}
