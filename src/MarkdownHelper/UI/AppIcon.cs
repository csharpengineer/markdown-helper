using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MarkdownHelper.UI
{
    /// <summary>Reads an app's icon and converts it to and from the form kept in settings.</summary>
    internal static class AppIcon
    {
        /// <summary>Reads the icon from an app's file, or returns null if it has none to give.</summary>
        public static BitmapSource FromFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                using (var icon = Icon.ExtractAssociatedIcon(path))
                {
                    var image = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    image.Freeze();
                    return image;
                }
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is UnauthorizedAccessException || e is COMException)
            {
                return null;
            }
        }

        public static string ToBase64(BitmapSource image)
        {
            if (image == null) return null;
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream);
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        /// <summary>Decodes a saved icon. Damaged data gives null, which shows as no icon.</summary>
        public static ImageSource FromBase64(string png)
        {
            if (string.IsNullOrEmpty(png)) return null;
            try
            {
                using (var stream = new MemoryStream(Convert.FromBase64String(png)))
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
            catch (Exception e) when (e is FormatException || e is NotSupportedException || e is IOException || e is ArgumentException)
            {
                return null;
            }
        }
    }
}
