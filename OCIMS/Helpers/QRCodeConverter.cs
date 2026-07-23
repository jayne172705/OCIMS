using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace eSureHi.Helpers
{
    public class QRCodeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return QRCodeHelper.GenerateQRCode(text);
            }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
