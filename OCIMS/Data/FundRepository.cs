using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using OCIMS.Models;

namespace OCIMS.Data
{
    public class FundRepository
    {
        public List<FundSource> GetAll()
        {
            var list = new List<FundSource>();
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT fund_id, fund_name, IFNULL(fund_type,'') AS fund_type,
                               amount, IFNULL(source,'') AS source,
                               date_received, fund_status, IFNULL(notes,'') AS notes
                        FROM ocims_fund_sources
                        ORDER BY fund_id DESC";

                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new FundSource
                            {
                                FundId = Convert.ToInt32(r["fund_id"]),
                                FundName = r["fund_name"].ToString(),
                                FundType = r["fund_type"].ToString(),
                                Amount = Convert.ToDecimal(r["amount"]),
                                DateReceived = r["date_received"] == DBNull.Value
                                    ? "—"
                                    : AppFormats.ToDisplayDate(Convert.ToDateTime(r["date_received"])),
                                Source = r["source"].ToString(),
                                FundStatus = r["fund_status"].ToString(),
                                Notes = r["notes"].ToString()
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

        public bool Save(FundSource f)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        INSERT INTO ocims_fund_sources
                            (fund_name, fund_type, amount, source,
                             date_received, fund_status, notes)
                        VALUES
                            (@name, @type, @amount, @source,
                             @received, @status, @notes)";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        AddParameters(cmd, f);
                        cmd.ExecuteNonQuery();
                        f.FundId = (int)cmd.LastInsertedId;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error saving fund: " + ex.Message, "eSureHi",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        public bool Update(FundSource f)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        UPDATE ocims_fund_sources SET
                            fund_name     = @name,
                            fund_type     = @type,
                            amount        = @amount,
                            source        = @source,
                            date_received = @received,
                            fund_status   = @status,
                            notes         = @notes
                        WHERE fund_id = @id";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        AddParameters(cmd, f);
                        cmd.Parameters.AddWithValue("@id", f.FundId);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error updating fund: " + ex.Message, "eSureHi",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        public bool Delete(int fundId)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    using (var cmd = new MySqlCommand("DELETE FROM ocims_fund_sources WHERE fund_id=@id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", fundId);
                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error deleting fund: " + ex.Message, "eSureHi",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        private static void AddParameters(MySqlCommand cmd, FundSource f)
        {
            DateTime received;
            object receivedValue = AppFormats.TryParseDisplayDate(f.DateReceived, out received)
                ? (object)received
                : DBNull.Value;

            cmd.Parameters.AddWithValue("@name", f.FundName);
            cmd.Parameters.AddWithValue("@type", f.FundType ?? "");
            cmd.Parameters.AddWithValue("@amount", f.Amount);
            cmd.Parameters.AddWithValue("@source", f.Source ?? "");
            cmd.Parameters.AddWithValue("@received", receivedValue);
            cmd.Parameters.AddWithValue("@status", f.FundStatus ?? "Active");
            cmd.Parameters.AddWithValue("@notes", f.Notes ?? "");
        }
    }
}
