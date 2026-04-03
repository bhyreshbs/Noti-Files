using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace FileActivityNotifier.UI.Dashboard;

public class DashboardViewModel : INotifyPropertyChanged
{
    private bool _isMonitoring = true;
    private int _renamedToday;
    private int _movedToday;
    private int _downloadedToday;

    public ObservableCollection<DashboardEventItem> AllEvents { get; } = new();

    public bool IsMonitoring
    {
        get => _isMonitoring;
        set => SetField(ref _isMonitoring, value);
    }

    public int RenamedToday
    {
        get => _renamedToday;
        private set => SetField(ref _renamedToday, value);
    }

    public int MovedToday
    {
        get => _movedToday;
        private set => SetField(ref _movedToday, value);
    }

    public int DownloadedToday
    {
        get => _downloadedToday;
        private set => SetField(ref _downloadedToday, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void AddEvent(DashboardEventItem eventItem)
    {
        if (eventItem is null)
        {
            return;
        }

        AllEvents.Insert(0, eventItem);
        RecalculateStats();
    }

    public void ClearEvents()
    {
        AllEvents.Clear();
        RecalculateStats();
    }

    public void RecalculateStats()
    {
        var today = DateTime.Today;
        var renamed = 0;
        var moved = 0;
        var downloaded = 0;

        foreach (var item in AllEvents)
        {
            if (item.Timestamp.Date != today)
            {
                continue;
            }

            switch (item.EventType)
            {
                case "Renamed":
                    renamed++;
                    break;
                case "Moved":
                    moved++;
                    break;
                case "Downloaded":
                    downloaded++;
                    break;
            }
        }

        RenamedToday = renamed;
        MovedToday = moved;
        DownloadedToday = downloaded;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public class DashboardEventItem
{
    private static readonly Brush SavedBadgeBackground = new SolidColorBrush(ColorHelper.FromArgb(255, 22, 101, 52));
    private static readonly Brush SavedBadgeForeground = new SolidColorBrush(ColorHelper.FromArgb(255, 74, 222, 128));
    private static readonly Brush MovedBadgeBackground = new SolidColorBrush(ColorHelper.FromArgb(255, 113, 63, 18));
    private static readonly Brush MovedBadgeForeground = new SolidColorBrush(ColorHelper.FromArgb(255, 251, 191, 36));
    private static readonly Brush DownloadedBadgeBackground = new SolidColorBrush(ColorHelper.FromArgb(255, 30, 58, 95));
    private static readonly Brush DownloadedBadgeForeground = new SolidColorBrush(ColorHelper.FromArgb(255, 96, 165, 250));

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public string FileExtension { get; set; } = string.Empty;

    public string EventType { get; set; } = "Saved";

    public DateTime Timestamp { get; set; } = DateTime.Now;

    public Brush EventBadgeBackground => EventType switch
    {
        "Moved" => MovedBadgeBackground,
        "Downloaded" => DownloadedBadgeBackground,
        _ => SavedBadgeBackground
    };

    public Brush EventBadgeForeground => EventType switch
    {
        "Moved" => MovedBadgeForeground,
        "Downloaded" => DownloadedBadgeForeground,
        _ => SavedBadgeForeground
    };
}
