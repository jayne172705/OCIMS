using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OCIMS.Data;

namespace OCIMS.Pages
{
    public partial class DashboardPage : Page
    {
        public DashboardPage()
        {
            InitializeComponent();
            LoadCalendar();
            LoadClientData();
        }

        private void LoadClientData()
        {
            try
            {
                var all = new EmployeeRepository().GetAll();
                TxtTotalClients.Text = all.Count.ToString();
                RecentClientsList.ItemsSource = all
                    .OrderByDescending(c => c.EmpId)
                    .Take(5)
                    .ToList();
            }
            catch
            {
                TxtTotalClients.Text = "0";
            }
        }

        private void LoadCalendar()
        {
            DateTime today = DateTime.Today;
            CalendarTitle.Text = today.ToString("MMMM yyyy");

            DateTime firstDay = new DateTime(today.Year, today.Month, 1);
            int startDay = (int)firstDay.DayOfWeek;
            int daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);

            CalendarGrid.Children.Clear();

            // Empty spaces before day 1
            for (int i = 0; i < startDay; i++)
            {
                CalendarGrid.Children.Add(new TextBlock());
            }

            // Day numbers
            for (int day = 1; day <= daysInMonth; day++)
            {
                bool isToday = (day == today.Day);

                Border border = new Border
                {
                    Width = 26,
                    Height = 26,
                    CornerRadius = new CornerRadius(13),
                    Margin = new Thickness(1),
                    Background = isToday
                        ? new SolidColorBrush(Color.FromRgb(46, 134, 222))
                        : Brushes.Transparent
                };

                TextBlock txt = new TextBlock
                {
                    Text = day.ToString(),
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = isToday
                        ? Brushes.White
                        : new SolidColorBrush(Color.FromRgb(74, 95, 116))
                };

                border.Child = txt;
                CalendarGrid.Children.Add(border);
            }
        }
    }
}
