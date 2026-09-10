using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using eSureHi.Data;
using eSureHi.Helpers;
using eSureHi.Models;
using eSureHi.Services;

namespace eSureHi.ViewModels.Admin
{
    public class ClaimSearchViewModel : ObservableObject
    {
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        private string? _selectedProgram;
        public string? SelectedProgram
        {
            get => _selectedProgram;
            set
            {
                if (SetProperty(ref _selectedProgram, value))
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        ExecuteSearch();
                    }
                }
            }
        }

        private readonly ObservableCollection<string> _programs = new();
        public ObservableCollection<string> Programs => _programs;

        public Action? CloseAction { get; set; }

        public RelayCommand SearchCommand { get; }
        public RelayCommand CancelCommand { get; }

        public ClaimSearchViewModel()
        {
            SearchCommand = new RelayCommand(ExecuteSearch);
            CancelCommand = new RelayCommand(() => CloseAction?.Invoke());

            _ = LoadProgramsAsync();
        }

        private async Task LoadProgramsAsync()
        {
            try
            {
                using var db = eSureHiDbContextFactory.Create();
                var distinctFunds = await db.Beneficiaries
                    .Where(b => b.IsActive && !string.IsNullOrEmpty(b.SourceOfFunds))
                    .Select(b => b.SourceOfFunds!.Trim())
                    .Distinct()
                    .ToListAsync();

                var progList = new List<string> { "All", "Job Order", "Casual", "Regular", "Captain" };
                foreach (var fund in distinctFunds)
                {
                    if (!progList.Contains(fund, StringComparer.OrdinalIgnoreCase))
                    {
                        progList.Add(fund);
                    }
                }

                _programs.Clear();
                foreach (var prog in progList)
                {
                    _programs.Add(prog);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load programs: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteSearch()
        {
            CloseAction?.Invoke();

            if (App.ActiveShell?.DataContext is AdminShellViewModel shellVm)
            {
                shellVm.NavigateToClaimsWithFilters(SearchText, SelectedProgram ?? "All");
            }
        }
    }
}
