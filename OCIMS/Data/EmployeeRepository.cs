using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using OCIMS.Models;

namespace OCIMS.Data
{
    public class EmployeeRepository
    {
        // ── GET ALL ──────────────────────────────────────────
        public List<Employee> GetAll()
        {
            var list = new List<Employee>();
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        SELECT e.emp_id, e.employee_no,
                               e.first_name, e.middle_name, e.last_name, e.suffix,
                               e.gender, e.civil_status,
                               e.email, e.phone_mobile, e.phone_office,
                               e.date_of_birth, e.date_hired,
                               e.position_title, e.employment_type,
                               e.employment_status,
                               IFNULL(e.address_line1,'') AS address_line1,
                               IFNULL(d.dept_name,'') AS dept_name
                        FROM employees e
                        LEFT JOIN departments d ON d.dept_id = e.dept_id
                        ORDER BY e.last_name, e.first_name";

                    using (var cmd = new MySqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var emp = new Employee
                            {
                                EmployeeNo = reader["employee_no"].ToString(),
                                FirstName = reader["first_name"].ToString(),
                                MiddleName = reader["middle_name"].ToString(),
                                LastName = reader["last_name"].ToString(),
                                Suffix = reader["suffix"].ToString(),
                                Gender = reader["gender"].ToString(),
                                CivilStatus = reader["civil_status"].ToString(),
                                Email = reader["email"].ToString(),
                                PhoneMobile = reader["phone_mobile"].ToString(),
                                PhoneOffice = reader["phone_office"].ToString(),
                                Address = reader["address_line1"].ToString(),
                                Department = reader["dept_name"].ToString(),
                                Position = reader["position_title"].ToString(),
                                EmploymentType = reader["employment_type"].ToString(),
                                EmploymentStatus = reader["employment_status"].ToString(),
                                DateHired = Convert.ToDateTime(reader["date_hired"]),
                                DateOfBirth = Convert.ToDateTime(reader["date_of_birth"])
                            };
                            list.Add(emp);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Database error: " + ex.Message,
                    "OCIMS",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            return list;
        }

        // ── SAVE ─────────────────────────────────────────────
        public bool Save(Employee emp)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    int deptId = GetDeptId(conn, emp.Department);

                    string sql = @"
                        INSERT INTO employees
                            (employee_no, first_name, middle_name, last_name, suffix,
                             gender, civil_status, email, phone_mobile, phone_office,
                             address_line1, date_of_birth, date_hired, position_title,
                             employment_type, employment_status, dept_id)
                        VALUES
                            (@empno,@fname,@mname,@lname,@suffix,
                             @gender,@civil,@email,@mobile,@office,
                             @address,@dob,@hired,@position,
                             @emptype,'Active',@deptid)";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@empno", emp.EmployeeNo);
                        cmd.Parameters.AddWithValue("@fname", emp.FirstName);
                        cmd.Parameters.AddWithValue("@mname", emp.MiddleName ?? "");
                        cmd.Parameters.AddWithValue("@lname", emp.LastName);
                        cmd.Parameters.AddWithValue("@suffix", emp.Suffix ?? "");
                        cmd.Parameters.AddWithValue("@gender", emp.Gender ?? "Other");
                        cmd.Parameters.AddWithValue("@civil", emp.CivilStatus ?? "Single");
                        cmd.Parameters.AddWithValue("@email", emp.Email);
                        cmd.Parameters.AddWithValue("@mobile", emp.PhoneMobile ?? "");
                        cmd.Parameters.AddWithValue("@office", emp.PhoneOffice ?? "");
                        cmd.Parameters.AddWithValue("@address", emp.Address ?? "");
                        cmd.Parameters.AddWithValue("@dob", emp.DateOfBirth);
                        cmd.Parameters.AddWithValue("@hired", emp.DateHired);
                        cmd.Parameters.AddWithValue("@position", emp.Position ?? "Client");
                        cmd.Parameters.AddWithValue("@emptype", emp.EmploymentType ?? "Regular");
                        cmd.Parameters.AddWithValue("@deptid", deptId);
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Error saving: " + ex.Message,
                    "OCIMS",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        // ── UPDATE ───────────────────────────────────────────
        public bool Update(Employee emp)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = @"
                        UPDATE employees SET
                            first_name    = @fname,
                            last_name     = @lname,
                            email         = @email,
                            phone_mobile  = @mobile,
                            address_line1 = @address
                        WHERE employee_no = @empno";

                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@fname", emp.FirstName);
                        cmd.Parameters.AddWithValue("@lname", emp.LastName);
                        cmd.Parameters.AddWithValue("@email", emp.Email);
                        cmd.Parameters.AddWithValue("@mobile", emp.PhoneMobile ?? "");
                        cmd.Parameters.AddWithValue("@address", emp.Address ?? "");
                        cmd.Parameters.AddWithValue("@empno", emp.EmployeeNo);
                        int rows = cmd.ExecuteNonQuery();
                        return rows > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Error updating: " + ex.Message,
                    "OCIMS",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        // ── DELETE ───────────────────────────────────────────
        public bool Delete(string empNo)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "DELETE FROM employees WHERE employee_no = @empno";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@empno", empNo);
                        int rows = cmd.ExecuteNonQuery();
                        return rows > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    "Error deleting: " + ex.Message,
                    "OCIMS",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        // ── CHECK DUPLICATE ──────────────────────────────────
        public bool EmployeeNoExists(string empNo)
        {
            try
            {
                using (var conn = DatabaseHelper.GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT COUNT(*) FROM employees WHERE employee_no=@empno";
                    using (var cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@empno", empNo);
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch { return false; }
        }

        // ── HELPER ───────────────────────────────────────────
        private int GetDeptId(MySqlConnection conn, string deptName)
        {
            try
            {
                string sql = "SELECT dept_id FROM departments WHERE dept_name=@name LIMIT 1";
                using (var cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@name", deptName ?? "");
                    var result = cmd.ExecuteScalar();
                    if (result != null)
                        return Convert.ToInt32(result);
                }
            }
            catch { }
            return 1;
        }
    }
}
