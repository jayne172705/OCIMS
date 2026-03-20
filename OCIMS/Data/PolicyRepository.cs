using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using OCIMS.Models;

namespace OCIMS.Data
{
    public class PolicyRepository
    {
        public List<Policy> GetAll()
        {
            var list = new List<Policy>();
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT policy_id, policy_code, policy_name, policy_type,
                               IFNULL(provider_name,'') AS provider_name,
                               coverage_amount,
                               IFNULL(expiry_date,'') AS expiry_date,
                               policy_status,
                               IFNULL(description,'') AS description
                        FROM insurance_policies
                        ORDER BY policy_name";

                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var p = new Policy
                            {
                                PolicyId = Convert.ToInt32(r["policy_id"]),
                                PolicyNo = r["policy_code"].ToString(),
                                PolicyName = r["policy_name"].ToString(),
                                PolicyType = r["policy_type"].ToString(),
                                Provider = r["provider_name"].ToString(),
                                CoverageAmount = Convert.ToDecimal(r["coverage_amount"]),
                                PolicyStatus = r["policy_status"].ToString(),
                                Description = r["description"].ToString()
                            };

                            var expiry = r["expiry_date"];
                            if (expiry != DBNull.Value && !string.IsNullOrEmpty(expiry.ToString()))
                                p.ExpiryDate = Convert.ToDateTime(expiry).ToString("MMM dd, yyyy");
                            else
                                p.ExpiryDate = "—";

                            list.Add(p);
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

        public bool Save(Policy p)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        INSERT INTO insurance_policies
                            (policy_code, policy_name, policy_type, provider_name,
                             coverage_amount, effective_date, expiry_date,
                             policy_status, description)
                        VALUES
                            (@code,@name,@type,@provider,
                             @coverage,@effective,@expiry,
                             'Active',@desc)";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@code", p.PolicyNo);
                        cmd.Parameters.AddWithValue("@name", p.PolicyName);
                        cmd.Parameters.AddWithValue("@type", p.PolicyType);
                        cmd.Parameters.AddWithValue("@provider", p.Provider ?? "");
                        cmd.Parameters.AddWithValue("@coverage", p.CoverageAmount);
                        cmd.Parameters.AddWithValue("@effective", DateTime.Today);
                        cmd.Parameters.AddWithValue("@expiry", p.ExpiryDate == "—" || string.IsNullOrEmpty(p.ExpiryDate)
                                                                    ? (object)DBNull.Value
                                                                    : DateTime.Parse(p.ExpiryDate));
                        cmd.Parameters.AddWithValue("@desc", p.Description ?? "");
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

        public bool Update(Policy p)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        UPDATE insurance_policies SET
                            policy_name    = @name,
                            policy_type    = @type,
                            provider_name  = @provider,
                            coverage_amount= @coverage,
                            policy_status  = @status,
                            description    = @desc
                        WHERE policy_id = @id";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@name", p.PolicyName);
                        cmd.Parameters.AddWithValue("@type", p.PolicyType);
                        cmd.Parameters.AddWithValue("@provider", p.Provider ?? "");
                        cmd.Parameters.AddWithValue("@coverage", p.CoverageAmount);
                        cmd.Parameters.AddWithValue("@status", p.PolicyStatus);
                        cmd.Parameters.AddWithValue("@desc", p.Description ?? "");
                        cmd.Parameters.AddWithValue("@id", p.PolicyId);
                        int rows = cmd.ExecuteNonQuery();
                        return rows > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error updating: " + ex.Message, "OCIMS",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        public bool Delete(int policyId)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "DELETE FROM insurance_policies WHERE policy_id = @id";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", policyId);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error deleting: " + ex.Message, "OCIMS",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        public bool PolicyNoExists(string code)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT COUNT(*) FROM insurance_policies WHERE policy_code=@code";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@code", code);
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch { return false; }
        }
    }
}
