using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FileActivityNotifier.UI.Toast;

public class ToastViewModel : INotifyPropertyChanged
{
    private static readonly Windows.UI.Color SavedBackgroundColor = ColorHelper.FromArgb(255, 22, 101, 52);
    private static readonly Windows.UI.Color SavedForegroundColor = ColorHelper.FromArgb(255, 74, 222, 128);
    private static readonly Windows.UI.Color RenamedBackgroundColor = ColorHelper.FromArgb(255, 22, 101, 52);
    private static readonly Windows.UI.Color RenamedForegroundColor = ColorHelper.FromArgb(255, 74, 222, 128);
    private static readonly Windows.UI.Color MovedBackgroundColor = ColorHelper.FromArgb(255, 113, 63, 18);
    private static readonly Windows.UI.Color MovedForegroundColor = ColorHelper.FromArgb(255, 251, 191, 36);
    private static readonly Windows.UI.Color DownloadedBackgroundColor = ColorHelper.FromArgb(255, 30, 58, 95);
    private static readonly Windows.UI.Color DownloadedForegroundColor = ColorHelper.FromArgb(255, 96, 165, 250);

    private string _fileName = string.Empty;
    private string _filePath = string.Empty;
    private string _fileExtension = string.Empty;
    private string _eventType = "Saved";
    private DateTime _timestamp = DateTime.Now;
    private string _timeAgo = "just now";
    private string _locationPreview = string.Empty;
    private ImageSource? _fileIconSource;
    private Brush? _badgeBackground;
    private Brush? _badgeForeground;
    private Brush? _accentBrush;

    public string FileName
    {
        get => _fileName;
        set => SetField(ref _fileName, value);
    }

    public string FilePath
    {
        get => _filePath;
        set => SetField(ref _filePath, value);
    }

    public string FileExtension
    {
        get => _fileExtension;
        set
        {
            if (SetField(ref _fileExtension, NormalizeExtension(value)))
            {
                OnPropertyChanged(nameof(FileTypeTag));
            }
        }
    }

    public string EventType
    {
        get => _eventType;
        set
        {
            if (SetField(ref _eventType, value))
            {
                UpdateBadgeColors(value);
            }
        }
    }

    public DateTime Timestamp
    {
        get => _timestamp;
        set
        {
            if (SetField(ref _timestamp, value))
            {
                UpdateTimeAgo();
            }
        }
    }

    public string TimeAgo
    {
        get => _timeAgo;
        private set => SetField(ref _timeAgo, value);
    }

    public ImageSource? FileIconSource
    {
        get => _fileIconSource;
        set => SetField(ref _fileIconSource, value);
    }

    public string LocationPreview
    {
        get => _locationPreview;
        private set => SetField(ref _locationPreview, value);
    }

    public string BadgeText => FileTypeTag;

    public string FileTypeTag
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FileExtension))
            {
                return "FILE";
            }

            return FileExtension.TrimStart('.').ToUpperInvariant();
        }
    }

    public string EventLabel => (EventType ?? string.Empty).Trim().ToUpperInvariant();

    public Brush? BadgeBackground
    {
        get => _badgeBackground;
        private set => SetField(ref _badgeBackground, value);
    }

    public Brush? BadgeForeground
    {
        get => _badgeForeground;
        private set => SetField(ref _badgeForeground, value);
    }

    public Brush? AccentBrush
    {
        get => _accentBrush;
        private set => SetField(ref _accentBrush, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static ToastViewModel Create(string fileName, string filePath, string extension, string eventType)
    {
        return new ToastViewModel
        {
            FileName = fileName,
            FilePath = filePath,
            FileExtension = extension,
            EventType = eventType,
            Timestamp = DateTime.Now,
            LocationPreview = BuildLocationPreview(filePath),
            FileIconSource = LoadIconForExtension(extension)
        };
    }

    public void UpdateTimeAgo()
    {
        var now = Timestamp.Kind == DateTimeKind.Utc ? DateTime.UtcNow : DateTime.Now;
        var diff = now - Timestamp;

        if (diff.TotalSeconds < 0)
        {
            TimeAgo = "just now";
            return;
        }

        if (diff.TotalSeconds < 60)
        {
            TimeAgo = "just now";
            return;
        }

        if (diff.TotalMinutes < 60)
        {
            var mins = Math.Max(1, (int)diff.TotalMinutes);
            TimeAgo = mins == 1 ? "1 min ago" : $"{mins} mins ago";
            return;
        }

        if (diff.TotalHours < 24)
        {
            var hours = Math.Max(1, (int)diff.TotalHours);
            TimeAgo = hours == 1 ? "1 hour ago" : $"{hours} hours ago";
            return;
        }

        TimeAgo = Timestamp.ToString("dd MMM, HH:mm");
    }

    private static ImageSource? LoadIconForExtension(string extension)
    {
        var ext = NormalizeExtension(extension);
        var iconFile = ext switch
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

        try
        {
            var image = new BitmapImage
            {
                DecodePixelWidth = 32,
                DecodePixelHeight = 32,
                UriSource = BuildAppxIconUri(iconFile)
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
                    localPath = Path.Combine(AppContext.BaseDirectory, iconFile);
                }

                if (!File.Exists(localPath))
                {
                    return null;
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
                return LoadFallbackIcon();
            }
        }
    }

    private static ImageSource? LoadFallbackIcon()
    {
        try
        {
            var image = new BitmapImage
            {
                DecodePixelWidth = 32,
                DecodePixelHeight = 32,
                UriSource = BuildAppxIconUri("AppIcon.ico")
            };
            return image;
        }
        catch
        {
            try
            {
                var localPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Icons", "AppIcon.ico");
                if (!File.Exists(localPath))
                {
                    return null;
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
                return null;
            }
        }
    }

    private static Uri BuildAppxIconUri(string iconFile)
    {
        return new Uri($"ms-appx:///Resources/Icons/{iconFile}", UriKind.Absolute);
    }

    private static string BuildLocationPreview(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ".../";
        }

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return ".../";
        }

        var folderName = Path.GetFileName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return ".../";
        }

        return $".../{folderName}/";
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

    private void UpdateBadgeColors(string eventType)
    {
        var normalized = (eventType ?? string.Empty).Trim();
        Windows.UI.Color backgroundColor;
        Windows.UI.Color foregroundColor;

        switch (normalized)
        {
            case "Renamed":
                backgroundColor = RenamedBackgroundColor;
                foregroundColor = RenamedForegroundColor;
                break;
            case "Moved":
                backgroundColor = MovedBackgroundColor;
                foregroundColor = MovedForegroundColor;
                break;
            case "Downloaded":
                backgroundColor = DownloadedBackgroundColor;
                foregroundColor = DownloadedForegroundColor;
                break;
            default:
                backgroundColor = SavedBackgroundColor;
                foregroundColor = SavedForegroundColor;
                break;
        }

        BadgeBackground = TryCreateBrush(backgroundColor);
        BadgeForeground = TryCreateBrush(foregroundColor);
        AccentBrush = BadgeForeground ?? TryCreateBrush(SavedForegroundColor);

        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(EventLabel));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static Brush? TryCreateBrush(Windows.UI.Color color)
    {
        try
        {
            return new SolidColorBrush(color);
        }
        catch
        {
            return null;
        }
    }
}
