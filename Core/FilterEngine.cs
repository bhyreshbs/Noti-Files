using System;
using System.Collections.Generic;
using System.IO;
using FileActivityNotifier.Models;

namespace FileActivityNotifier.Core;

public class FilterEngine
{
    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "tmp", "temp", "part", "crdownload"
    };

    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(2);

    private readonly object _sync = new();
    private readonly Dictionary<string, DateTime> _recentEvents = new(StringComparer.OrdinalIgnoreCase);

    public bool ShouldShow(FileEvent fileEvent, AppSettings settings)
    {
        if (fileEvent == null || settings == null)
        {
            return false;
        }

        if (!settings.IsMonitoringEnabled)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(fileEvent.FileName) || string.IsNullOrWhiteSpace(fileEvent.FilePath))
        {
            return false;
        }

        if (fileEvent.FileName.StartsWith("~", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var extension = NormalizeExtension(fileEvent.Extension);
        if (IgnoredExtensions.Contains(extension))
        {
            return false;
        }

        if (!IsAllowedExtension(extension, settings.AllowedExtensions))
        {
            return false;
        }

        if (ContainsIgnoredPathSegment(fileEvent.FilePath))
        {
            return false;
        }

        if (IsDuplicate(fileEvent))
        {
            return false;
        }

        if (HasHiddenOrSystemAttributes(fileEvent.FilePath))
        {
            return false;
        }

        if (File.Exists(fileEvent.FilePath))
        {
            try
            {
                var info = new FileInfo(fileEvent.FilePath);
                if (info.Length < 100)
                {
                    return false;
                }
            }
            catch
            {
                // If file metadata can't be read (race/permissions), keep event non-blocking.
            }
        }

        return true;
    }

    private bool IsDuplicate(FileEvent fileEvent)
    {
        var now = DateTime.UtcNow;
        var key = $"{fileEvent.FilePath}|{fileEvent.EventType}";

        lock (_sync)
        {
            var staleKeys = new List<string>();
            foreach (var pair in _recentEvents)
            {
                if (now - pair.Value > DuplicateWindow)
                {
                    staleKeys.Add(pair.Key);
                }
            }

            foreach (var stale in staleKeys)
            {
                _recentEvents.Remove(stale);
            }

            if (_recentEvents.TryGetValue(key, out var previous) && now - previous <= DuplicateWindow)
            {
                return true;
            }

            _recentEvents[key] = now;
            return false;
        }
    }

    private static bool IsAllowedExtension(string extension, List<string> allowedExtensions)
    {
        foreach (var ext in allowedExtensions)
        {
            if (NormalizeExtension(ext).Equals(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeExtension(string extension)
    {
        return (extension ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
    }

    private static bool ContainsIgnoredPathSegment(string fullPath)
    {
        return fullPath.Contains("\\AppData\\", StringComparison.OrdinalIgnoreCase)
               || fullPath.Contains("\\Windows\\", StringComparison.OrdinalIgnoreCase)
               || fullPath.Contains("\\Program Files\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasHiddenOrSystemAttributes(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            var attributes = File.GetAttributes(fullPath);
            return attributes.HasFlag(FileAttributes.Hidden)
                   || attributes.HasFlag(FileAttributes.System)
                   || attributes.HasFlag(FileAttributes.Temporary);
        }
        catch
        {
            return false;
        }
    }
}
