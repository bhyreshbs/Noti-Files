using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FileActivityNotifier.Models;

/// <summary>
/// Application settings model persisted to settings.json
/// Represents user preferences for monitoring, notifications, and UI behavior.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Folder paths to monitor for file activity
    /// </summary>
    public List<string> WatchedFolders { get; set; } = new();

    /// <summary>
    /// File extensions to include (without dot: "pdf", "docx", "jpg")
    /// </summary>
    public List<string> AllowedExtensions { get; set; } = new();

    /// <summary>
    /// How long toast notifications stay visible (seconds)
    /// Range: 2.0-15.0 seconds
    /// </summary>
    public double NotificationDurationSeconds { get; set; } = 5;

    /// <summary>
    /// Maximum number of toasts to stack on screen
    /// Range: 1-10
    /// </summary>
    public int MaxStackCount { get; set; } = 5;

    /// <summary>
    /// Current theme preference: "Dark" or "Light"
    /// </summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>
    /// Whether to start app on Windows startup
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Whether file monitoring is currently enabled
    /// </summary>
    public bool IsMonitoringEnabled { get; set; } = true;

    /// <summary>
    /// Create default settings with common folders and extensions
    /// </summary>
    public static AppSettings CreateDefaults()
    {
        return new AppSettings
        {
            WatchedFolders = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            },
            AllowedExtensions = new List<string>
            {
                "pdf", "docx", "xlsx", "jpg", "png", "gif", "mp4", "zip", "txt", "pptx"
            },
            NotificationDurationSeconds = 5,
            MaxStackCount = 5,
            Theme = "Dark",
            StartWithWindows = false,
            IsMonitoringEnabled = true
        };
    }

    /// <summary>
    /// Validate settings for consistency and safety
    /// </summary>
    public bool Validate()
    {
        // Validate duration range
        if (NotificationDurationSeconds < 2 || NotificationDurationSeconds > 15)
            return false;

        // Validate stack count range
        if (MaxStackCount < 1 || MaxStackCount > 10)
            return false;

        // Validate theme
        if (string.IsNullOrWhiteSpace(Theme) ||
            (!Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) && 
             !Theme.Equals("Light", StringComparison.OrdinalIgnoreCase)))
            return false;

        // Collections must be initialized.
        if (WatchedFolders == null || AllowedExtensions == null)
            return false;

        return true;
    }

    /// <summary>
    /// Normalize settings after deserialization from JSON
    /// </summary>
    public void Normalize()
    {
        // Ensure Theme is capitalized properly
        if (!string.IsNullOrEmpty(Theme))
        {
            Theme = Theme.Equals("light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
        }
        else
        {
            Theme = "Dark";
        }

        // Normalize extensions to lowercase
        if (AllowedExtensions != null)
        {
            AllowedExtensions = AllowedExtensions
                .Select(ext => ext.TrimStart('.').ToLower())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else
        {
            AllowedExtensions = new List<string>();
        }

        NotificationDurationSeconds = Math.Clamp(Math.Round(NotificationDurationSeconds * 2, MidpointRounding.AwayFromZero) / 2, 2, 15);

        // Remove empty folder paths
        if (WatchedFolders != null)
        {
            WatchedFolders = WatchedFolders
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else
        {
            WatchedFolders = new List<string>();
        }
    }

    /// <summary>
    /// Override ToString for debugging
    /// </summary>
    public override string ToString() =>
        $"Theme: {Theme} | Folders: {WatchedFolders.Count} | Extensions: {AllowedExtensions.Count} | Duration: {NotificationDurationSeconds}s";
}
