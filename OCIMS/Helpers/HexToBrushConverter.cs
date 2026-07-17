using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace eSureHi.Helpers
{
    public class HexToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType,
            object? parameter, CultureInfo culture)
        {
            try
            {
                if (value is string hex)
                    return new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString(hex));
            }
            catch { }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object? value, Type targetType,
            object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
