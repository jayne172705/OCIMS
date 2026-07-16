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
                               expiry_date,
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
                            p.ExpiryDate = expiry == DBNull.Value
                                ? "—"
                                : AppFormats.ToDisplayDate(Convert.ToDateTime(expiry));

                            list.Add(p);
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
                        DateTime expiryDate;
                        cmd.Parameters.AddWithValue("@effective", DateTime.Today);
                        cmd.Parameters.AddWithValue("@expiry", AppFormats.TryParseDisplayDate(p.ExpiryDate, out expiryDate)
                                                                    ? (object)expiryDate
                                                                    : DBNull.Value);
                        cmd.Parameters.AddWithValue("@desc", p.Description ?? "");
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
                            expiry_date    = @expiry,
                            policy_status  = @status,
                            description    = @desc
                        WHERE policy_id = @id";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        DateTime expiryDate;
                        cmd.Parameters.AddWithValue("@expiry", AppFormats.TryParseDisplayDate(p.ExpiryDate, out expiryDate)
                                                                    ? (object)expiryDate
                                                                    : DBNull.Value);
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
                System.Windows.MessageBox.Show("Error updating: " + ex.Message, "eSureHi",
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
                System.Windows.MessageBox.Show("Error deleting: " + ex.Message, "eSureHi",
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
