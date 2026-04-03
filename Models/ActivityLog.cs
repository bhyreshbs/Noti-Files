using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;

namespace FileActivityNotifier.Models;

/// <summary>
/// Manages the activity log — a list of FileEvent objects persisted to JSON
/// Provides CRUD operations: add, retrieve, filter, and clear events
/// </summary>
public class ActivityLog
{
    /// <summary>
    /// In-memory collection of all logged events
    /// </summary>
    public List<FileEvent> Events { get; set; } = new();

    /// <summary>
    /// Maximum number of events to keep in memory (prevents bloat)
    /// Oldest events are removed when limit is exceeded
    /// </summary>
    private const int MaxEventsInMemory = 1000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    /// <summary>
    /// Get the standard activity log file path
    /// Location: %AppData%\Noti-Files\activity_log.json
    /// </summary>
    public static string GetActivityLogPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var notiFilesFolder = Path.Combine(appDataPath, "Noti-Files");
        return Path.Combine(notiFilesFolder, "activity_log.json");
    }

    /// <summary>
    /// Add a new event to the log (in-memory only)
    /// Inserts at index 0 so newest events appear first
    /// Removes oldest events if max count exceeded
    /// </summary>
    public void AddEvent(FileEvent fileEvent)
    {
        if (fileEvent == null)
            return;

        Events.Insert(0, fileEvent);

        // Remove oldest events if exceeded max
        if (Events.Count > MaxEventsInMemory)
        {
            Events.RemoveRange(MaxEventsInMemory, Events.Count - MaxEventsInMemory);
        }
    }

    /// <summary>
    /// Get all events from today
    /// </summary>
    public IEnumerable<FileEvent> GetTodayEvents()
    {
        var today = DateTime.UtcNow.Date;
        return Events.Where(e => e.Timestamp.Date == today);
    }

    /// <summary>
    /// Get all events from the past 7 days
    /// </summary>
    public IEnumerable<FileEvent> GetWeekEvents()
    {
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        return Events.Where(e => e.Timestamp >= sevenDaysAgo);
    }

    /// <summary>
    /// Get events filtered by event type
    /// </summary>
    public IEnumerable<FileEvent> GetEventsByType(FileEventType eventType)
    {
        return Events.Where(e => e.EventType == eventType);
    }

    /// <summary>
    /// Get events filtered by event type (overload for string)
    /// </summary>
    public IEnumerable<FileEvent> GetEventsByType(string eventType)
    {
        if (!Enum.TryParse<FileEventType>(eventType, ignoreCase: true, out var type))
            return Enumerable.Empty<FileEvent>();
        return GetEventsByType(type);
    }

    /// <summary>
    /// Get events filtered by file extension
    /// </summary>
    public IEnumerable<FileEvent> GetEventsByExtension(string extension)
    {
        var ext = extension.TrimStart('.');
        return Events.Where(e => e.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Search events by file name or path (case-insensitive substring match)
    /// </summary>
    public IEnumerable<FileEvent> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Events;

        var lowerQuery = query.ToLower();
        return Events.Where(e => 
            (!string.IsNullOrEmpty(e.FileName) && e.FileName.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase)) || 
            (!string.IsNullOrEmpty(e.FilePath) && e.FilePath.Contains(lowerQuery, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Clear all events from the log (in-memory)
    /// </summary>
    public void Clear()
    {
        Events.Clear();
    }

    /// <summary>
    /// Get event count
    /// </summary>
    public int Count => Events.Count;

    /// <summary>
    /// Load activity log from JSON file
    /// Returns empty log if file doesn't exist or on error
    /// </summary>
    public static ActivityLog LoadFromFile()
    {
        var filePath = GetActivityLogPath();

        if (!File.Exists(filePath))
        {
            return new ActivityLog();
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var events = JsonSerializer.Deserialize<List<FileEvent>>(json, JsonOptions) ?? new List<FileEvent>();
            return new ActivityLog { Events = events };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ActivityLog] Error loading: {ex.Message}");
            return new ActivityLog();
        }
    }

    /// <summary>
    /// Save activity log to JSON file
    /// Creates directory if it doesn't exist
    /// </summary>
    public void SaveToFile()
    {
        var filePath = GetActivityLogPath();
        var directory = Path.GetDirectoryName(filePath);

        try
        {
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(Events, JsonOptions);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ActivityLog] Error saving: {ex.Message}");
        }
    }

    /// <summary>
    /// Get summary statistics
    /// </summary>
    public (int Total, int Today, int Saved, int Moved, int Downloaded) GetStats()
    {
        var today = GetTodayEvents().Count();
        var saved = GetEventsByType("Saved").Count();
        var moved = GetEventsByType("Moved").Count();
        var downloaded = GetEventsByType("Downloaded").Count();

        return (Events.Count, today, saved, moved, downloaded);
    }

    /// <summary>
    /// Override ToString for debugging
    /// </summary>
    public override string ToString() => $"ActivityLog | Events: {Events.Count}";
}
