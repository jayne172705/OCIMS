using System;
using System.Globalization;
using System.Windows.Data;

namespace eSureHi.Helpers
{
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType,
            object? parameter, CultureInfo culture)
        {
            bool isNotNull = value is not null;
            if (targetType == typeof(System.Windows.Visibility))
                return isNotNull ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            return isNotNull;
        }

        public object ConvertBack(object? value, Type targetType,
            object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
