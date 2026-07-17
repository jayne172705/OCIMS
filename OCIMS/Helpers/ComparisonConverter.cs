using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace eSureHi.Helpers
{
    /// <summary>
    /// Returns Visible if value equals parameter (as string), else Collapsed.
    /// </summary>
    public class ComparisonConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return Visibility.Collapsed;
            return value.ToString() == parameter.ToString() ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
