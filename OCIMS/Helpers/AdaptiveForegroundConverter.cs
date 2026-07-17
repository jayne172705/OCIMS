using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace eSureHi.Helpers
{
    public class AdaptiveForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                Color color = brush.Color;
                
                // Calculate luminance (ITU-R BT.709)
                double luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255;
                
                // If the background is light, return dark text. If dark, return light text.
                if (luminance > 0.5)
                {
                    // Dark navy text for light backgrounds (branding consistent)
                    return new SolidColorBrush(Color.FromRgb(16, 37, 64)); // #102540
                }
                else
                {
                    // White text for dark backgrounds
                    return Brushes.White;
                }
            }

            return Brushes.White; // Default
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
