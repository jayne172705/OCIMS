using System;
using System.Threading.Tasks;
using System.Windows;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;

namespace eSureHi.ViewModels.Admin
{
    public class TrainingFormViewModel : ObservableObject
    {
        private readonly int _empId;

        private string _title = string.Empty;
        private string _description = string.Empty;
        private DateTime? _dateAttended = DateTime.Today;
        private int _durationHours;
        private string _instructorOrVenue = string.Empty;

        public string Title { get => _title; set => SetProperty(ref _title, value); }
        public string Description { get => _description; set => SetProperty(ref _description, value); }
        public DateTime? DateAttended { get => _dateAttended; set => SetProperty(ref _dateAttended, value); }
        public int DurationHours { get => _durationHours; set => SetProperty(ref _durationHours, value); }
        public string InstructorOrVenue { get => _instructorOrVenue; set => SetProperty(ref _instructorOrVenue, value); }

        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }
        public Action? CloseAction { get; set; }
        public Action? OnSaveSuccess { get; set; }

        public TrainingFormViewModel(int empId)
        {
            _empId = empId;
            SaveCommand = new RelayCommand(async () => await SaveAsync());
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());
        }

        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Title)) { MessageBox.Show("Title is required."); return; }
            if (DateAttended == null) { MessageBox.Show("Date attended is required."); return; }
            if (DurationHours <= 0) { MessageBox.Show("Duration hours must be > 0."); return; }

            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var t = new Training
                {
                    EmployeeId = _empId,
                    Title = Title.Trim(),
                    Description = Description?.Trim(),
                    DateAttended = DateOnly.FromDateTime(DateAttended.Value),
                    DurationHours = DurationHours,
                    InstructorOrVenue = InstructorOrVenue?.Trim()
                };
                db.Trainings.Add(t);
                await db.SaveChangesAsync();

                OnSaveSuccess?.Invoke();
                CloseAction?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed: {ex.Message}");
            }
        }
    }
}
