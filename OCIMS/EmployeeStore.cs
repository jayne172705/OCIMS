using System.Collections.ObjectModel;
using OCIMS.Models;

namespace OCIMS
{
    public static class EmployeeStore
    {
        // Shared list — makita sa tanan nga pages
        public static ObservableCollection<Employee> Employees
        { get; set; } = new ObservableCollection<Employee>();
    }
}