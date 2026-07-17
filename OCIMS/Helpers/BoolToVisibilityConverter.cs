using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace eSureHi.Helpers
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType,
            object? parameter, CultureInfo culture)
        {
            bool invert = parameter?.ToString() == "Invert";
            bool boolVal = value is bool b && b;
            if (invert) boolVal = !boolVal;
            return boolVal ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType,
            object? parameter, CultureInfo culture)
        {
            bool invert = parameter?.ToString() == "Invert";
            bool isVisible = value is Visibility.Visible;
            return invert ? !isVisible : isVisible;
        }
    }
}
