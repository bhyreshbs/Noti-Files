using System;
using System.IO;

namespace FileActivityNotifier.Models;

/// <summary>
/// Enumeration of file event types
/// </summary>
public enum FileEventType
{
    Saved,
    Renamed,
    Moved,
    Downloaded
}

/// <summary>
/// Represents a single file system event (save, move, download, etc.)
/// This is the core data model that flows through the entire application.
/// </summary>
public class FileEvent
{
    /// <summary>
    /// File name with extension (e.g., "Report.pdf")
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Full file path (e.g., "C:\Users\Downloads\Report.pdf")
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// File extension without dot (e.g., "pdf", "docx", "jpg")
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// Type of event: Saved, Moved, or Downloaded
    /// </summary>
    public FileEventType EventType { get; set; } = FileEventType.Saved;

    /// <summary>
    /// UTC timestamp when event occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    public FileEvent()
    {
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to create a FileEvent from file system event details
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if filePath is null or empty</exception>
    public static FileEvent Create(string filePath, FileEventType eventType, DateTime? timestamp = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        var fileName = Path.GetFileName(filePath);
        var extension = Path.GetExtension(filePath).TrimStart('.').ToLower();

        return new FileEvent
        {
            FileName = fileName,
            FilePath = filePath,
            Extension = extension,
            EventType = eventType,
            Timestamp = timestamp ?? DateTime.UtcNow
        };
    }

    /// <summary>
    /// Factory method overload accepting string event type (for JSON deserialization)
    /// </summary>
    public static FileEvent Create(string filePath, string eventType, DateTime? timestamp = null)
    {
        if (!Enum.TryParse<FileEventType>(eventType, ignoreCase: true, out var parsedType))
            parsedType = FileEventType.Saved;

        return Create(filePath, parsedType, timestamp);
    }

    /// <summary>
    /// Override ToString for debugging
    /// </summary>
    public override string ToString() => $"{EventType} | {FileName} | {Timestamp:yyyy-MM-dd HH:mm:ss}";
}
