using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace eSureHi.Helpers
{
    public class PathToImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType,
            object? parameter, CultureInfo culture)
        {
            try
            {
                if (value is string path &&
                    !string.IsNullOrWhiteSpace(path) &&
                    File.Exists(path))
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(path, UriKind.Absolute);
                    image.EndInit();
                    return image;
                }
            }
            catch { }
            return null;
        }

        public object? ConvertBack(object? value, Type targetType,
            object? parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
