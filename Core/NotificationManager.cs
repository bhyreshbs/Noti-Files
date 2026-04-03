using System;
using System.Linq;
using FileActivityNotifier.Models;
using FileActivityNotifier.UI.Toast;

namespace FileActivityNotifier.Core;

public class NotificationManager
{
    private readonly object _sync = new();
    private readonly ActivityLog _activityLog;
    private readonly ToastStackManager _toastStackManager;

    public NotificationManager(ToastStackManager toastStackManager)
    {
        _toastStackManager = toastStackManager;
        _activityLog = ActivityLog.LoadFromFile();
    }

    public void Show(FileEvent fileEvent, AppSettings settings, bool showToast = true)
    {
        if (fileEvent == null || settings == null)
        {
            return;
        }

        lock (_sync)
        {
            _activityLog.AddEvent(fileEvent);
            _activityLog.SaveToFile();
        }

        if (!showToast)
        {
            return;
        }

        if (!ShouldShowToast(fileEvent.EventType))
        {
            return;
        }

        _toastStackManager.ShowToast(
            () => ToastViewModel.Create(
                fileEvent.FileName,
                fileEvent.FilePath,
                fileEvent.Extension,
                fileEvent.EventType.ToString()),
            settings.NotificationDurationSeconds,
            settings.MaxStackCount);
    }

    public ActivityLog SnapshotLog()
    {
        lock (_sync)
        {
            return new ActivityLog { Events = _activityLog.Events.ToList() };
        }
    }

    private static bool ShouldShowToast(FileEventType eventType)
    {
        return eventType == FileEventType.Saved
               || eventType == FileEventType.Renamed
               || eventType == FileEventType.Moved
               || eventType == FileEventType.Downloaded;
    }
}
