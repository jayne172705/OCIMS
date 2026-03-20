using System;
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
            LoadRecentClients();
            LoadStats();
        }

        private void LoadStats()
        {
            try
            {
                var repo = new EmployeeRepository();
                var all = repo.GetAll();
                TxtTotalClients.Text = all.Count.ToString();
            }
            catch
            {
                TxtTotalClients.Text = "0";
            }
        }

        private void LoadRecentClients()
        {
            try
            {
                var repo = new EmployeeRepository();
                var list = repo.GetAll();
                if (list.Count > 5)
                    list = list.GetRange(0, 5);
                RecentClientsList.ItemsSource = list;
            }
            catch { }
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
