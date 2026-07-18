using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Models;
using eSureHi.Helpers;

namespace eSureHi.ViewModels.Admin.Dialogs
{
    public class FamilyHeadsViewModel
    {
        public string BarangayDisplay { get; }
        public ObservableCollection<Employee> FamilyHeads { get; } = new();

        private readonly string _barangay;

        public ICommand OpenProfileCommand { get; }
        public Action<Employee>? OpenProfileRequested { get; set; }

        public FamilyHeadsViewModel(string barangay)
        {
            _barangay = barangay;
            BarangayDisplay = $"Barangay {barangay}";
            
            OpenProfileCommand = new RelayCommand<Employee>(head => 
            {
                if (head != null)
                {
                    OpenProfileRequested?.Invoke(head);
                }
            });
        }

        public async Task LoadAsync()
        {
            using var db = eSureHiDbContextFactory.CreateCloud();
            var heads = await db.Employees
                .Where(emp => emp.Barangay == _barangay)
                .OrderBy(emp => emp.LastName)
                .ThenBy(emp => emp.FirstName)
                .ToListAsync();

            FamilyHeads.Clear();
            foreach (var head in heads)
            {
                FamilyHeads.Add(head);
            }
        }
    }
}
