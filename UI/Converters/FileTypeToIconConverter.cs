using System;
using System.Globalization;
using System.IO;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FileActivityNotifier.UI.Converters;

public class FileTypeToIconConverter : IValueConverter
{
    private static readonly ImageSource FallbackImage = LoadFallbackImage();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var extension = NormalizeExtension(value?.ToString());

        var iconFile = extension switch
        {
            ".doc" or ".docx" => "docx.ico",
            ".xls" or ".xlsx" => "xlsx.ico",
            ".pdf" => "pdf.ico",
            ".jpg" or ".jpeg" or ".png" or ".gif" => "img.ico",
            ".mp4" or ".mov" or ".avi" => "video.ico",
            ".zip" or ".rar" or ".7z" => "zip.ico",
            ".exe" or ".msi" => "exe.ico",
            ".txt" or ".md" or ".csv" => "txt.ico",
            _ => "generic.ico"
        };

        return LoadImage(iconFile);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }

    private static string NormalizeExtension(string? extension)
    {
        var normalized = (extension ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            return string.Empty;
        }

        return normalized.StartsWith('.') ? normalized : $".{normalized}";
    }

    private static ImageSource LoadImage(string iconFile)
    {
        try
        {
            var image = new BitmapImage
            {
                DecodePixelWidth = 32,
                DecodePixelHeight = 32,
                UriSource = new Uri($"ms-appx:///Resources/Icons/{iconFile}", UriKind.Absolute)
            };
            return image;
        }
        catch
        {
            try
            {
                var localPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Icons", iconFile);
                if (!File.Exists(localPath))
                {
                    return FallbackImage;
                }

                var image = new BitmapImage
                {
                    DecodePixelWidth = 32,
                    DecodePixelHeight = 32,
                    UriSource = new Uri(localPath, UriKind.Absolute)
                };
                return image;
            }
            catch
            {
                return FallbackImage;
            }
        }
    }

    private static ImageSource LoadFallbackImage()
    {
        try
        {
            var image = new BitmapImage
            {
                DecodePixelWidth = 32,
                DecodePixelHeight = 32,
                UriSource = new Uri("ms-appx:///Resources/Icons/AppIcon.ico", UriKind.Absolute)
            };
            return image;
        }
        catch
        {
            return new BitmapImage();
        }
    }
}
