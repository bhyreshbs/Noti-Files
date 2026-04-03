using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace FileActivityNotifier.UI.Converters;

public class DateTimeToAgoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not DateTime timestamp)
        {
            return string.Empty;
        }

        var now = timestamp.Kind == DateTimeKind.Utc ? DateTime.UtcNow : DateTime.Now;
        var diff = now - timestamp;

        if (diff.TotalSeconds < 0)
        {
            return "just now";
        }

        if (diff.TotalSeconds < 60)
        {
            return "just now";
        }

        if (diff.TotalMinutes < 60)
        {
            var minutes = Math.Max(1, (int)diff.TotalMinutes);
            return minutes == 1 ? "1 min ago" : $"{minutes} mins ago";
        }

        if (diff.TotalHours < 24)
        {
            var hours = Math.Max(1, (int)diff.TotalHours);
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        if (timestamp.Date == now.Date.AddDays(-1))
        {
            return "Yesterday";
        }

        if (diff.TotalDays < 7)
        {
            var days = Math.Max(1, (int)diff.TotalDays);
            return days == 1 ? "1 day ago" : $"{days} days ago";
        }

        return timestamp.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
