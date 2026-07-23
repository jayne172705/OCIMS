using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace eSureHi.Helpers
{
    public static class QRCodeHelper
    {
        public static BitmapImage? GenerateQRCode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            using (var qrGenerator = new QRCodeGenerator())
            {
                var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new BitmapByteQRCode(qrCodeData);
                byte[] qrCodeBytes = qrCode.GetGraphic(20);

                using (var ms = new MemoryStream(qrCodeBytes))
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = ms;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze(); // Needed for cross-thread operations
                    return bitmapImage;
                }
            }
        }
    }
}
