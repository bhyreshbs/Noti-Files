using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileActivityNotifier.Models;

namespace FileActivityNotifier.Core;

public class EventProcessor
{
    private static readonly TimeSpan MoveCandidateWindow = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan TemporaryDownloadWindow = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan PostResolutionSuppressionWindow = TimeSpan.FromSeconds(2);

    private static readonly HashSet<string> DownloadTemporaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".crdownload", ".part", ".tmp", ".download", ".partial", ".opdownload"
    };

    private readonly object _sync = new();
    private readonly Dictionary<string, List<DeletedCandidate>> _deletedCandidatesByFileName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TemporaryDownloadArtifact>> _temporaryArtifactsByDirectory = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ResolvedTarget> _recentResolvedTargets = new(StringComparer.OrdinalIgnoreCase);

    public FileEvent? Process(FileSystemEventArgs args)
    {
        if (args is null || string.IsNullOrWhiteSpace(args.FullPath))
        {
            return null;
        }

        var now = DateTime.UtcNow;

        lock (_sync)
        {
            CleanupState(now);

            if (args.ChangeType == WatcherChangeTypes.Deleted)
            {
                TrackDeletedCandidateLocked(args.FullPath, now);

                if (IsDownloadsPath(args.FullPath) && IsTemporaryDownloadPath(args.FullPath))
                {
                    TrackTemporaryDownloadArtifactLocked(args.FullPath, now);
                }

                return null;
            }

            if ((args.ChangeType == WatcherChangeTypes.Created || args.ChangeType == WatcherChangeTypes.Changed)
                && ShouldSuppressDueToRecentResolvedTargetLocked(args.FullPath, now))
            {
                return null;
            }

            var eventType = args.ChangeType switch
            {
                WatcherChangeTypes.Created => ResolveCreatedEventTypeLocked(args.FullPath, now),
                WatcherChangeTypes.Changed => FileEventType.Saved,
                _ => (FileEventType?)null
            };

            if (eventType == null)
            {
                return null;
            }

            if (eventType != FileEventType.Saved)
            {
                TrackResolvedTargetLocked(args.FullPath, eventType.Value, now);
            }

            return FileEvent.Create(args.FullPath, eventType.Value, now);
        }
    }

    public FileEvent? Process(RenamedEventArgs args)
    {
        if (args is null || string.IsNullOrWhiteSpace(args.FullPath) || string.IsNullOrWhiteSpace(args.OldFullPath))
        {
            return null;
        }

        var now = DateTime.UtcNow;

        lock (_sync)
        {
            CleanupState(now);

            if (IsDownloadsPath(args.OldFullPath) && IsTemporaryDownloadPath(args.OldFullPath))
            {
                TrackTemporaryDownloadArtifactLocked(args.OldFullPath, now);
            }

            var eventType = ResolveRenamedEventTypeLocked(args, now);
            TrackResolvedTargetLocked(args.FullPath, eventType, now);

            return FileEvent.Create(args.FullPath, eventType, now);
        }
    }

    private static bool IsDownloadsPath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        var separator = Path.DirectorySeparatorChar;
        return fullPath.Contains($"{separator}Downloads{separator}", StringComparison.OrdinalIgnoreCase);
    }

    private FileEventType ResolveCreatedEventTypeLocked(string fullPath, DateTime nowUtc)
    {
        var wasLikelyMoved = TryConsumeDeletedCandidateLocked(fullPath);
        if (wasLikelyMoved)
        {
            return FileEventType.Moved;
        }

        if (IsDownloadsPath(fullPath) && TryConsumeRecentTemporaryArtifactLocked(fullPath))
        {
            return FileEventType.Downloaded;
        }

        return FileEventType.Saved;
    }

    private FileEventType ResolveRenamedEventTypeLocked(RenamedEventArgs args, DateTime nowUtc)
    {
        if (IsDownloadsPath(args.FullPath)
            && (IsTemporaryDownloadPath(args.OldFullPath)
                || TryConsumeRecentTemporaryArtifactLocked(args.FullPath)))
        {
            return FileEventType.Downloaded;
        }

        var oldDirectory = Path.GetDirectoryName(args.OldFullPath) ?? string.Empty;
        var newDirectory = Path.GetDirectoryName(args.FullPath) ?? string.Empty;

        if (!oldDirectory.Equals(newDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return FileEventType.Moved;
        }

        return FileEventType.Renamed;
    }

    private static bool IsTemporaryDownloadPath(string fullPath)
    {
        var extension = Path.GetExtension(fullPath);
        return DownloadTemporaryExtensions.Contains(extension);
    }

    private void TrackDeletedCandidateLocked(string fullPath, DateTime nowUtc)
    {
        var fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        if (!_deletedCandidatesByFileName.TryGetValue(fileName, out var candidates))
        {
            candidates = new List<DeletedCandidate>();
            _deletedCandidatesByFileName[fileName] = candidates;
        }

        candidates.Add(new DeletedCandidate(fullPath, nowUtc));
    }

    private bool TryConsumeDeletedCandidateLocked(string createdPath)
    {
        var fileName = Path.GetFileName(createdPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (!_deletedCandidatesByFileName.TryGetValue(fileName, out var candidates) || candidates.Count == 0)
        {
            return false;
        }

        var matchedCandidate = candidates.FirstOrDefault(candidate =>
            !candidate.FullPath.Equals(createdPath, StringComparison.OrdinalIgnoreCase));

        if (matchedCandidate.FullPath == null)
        {
            return false;
        }

        candidates.Remove(matchedCandidate);
        if (candidates.Count == 0)
        {
            _deletedCandidatesByFileName.Remove(fileName);
        }

        return true;
    }

    private void TrackTemporaryDownloadArtifactLocked(string fullPath, DateTime nowUtc)
    {
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        if (!_temporaryArtifactsByDirectory.TryGetValue(directory, out var artifacts))
        {
            artifacts = new List<TemporaryDownloadArtifact>();
            _temporaryArtifactsByDirectory[directory] = artifacts;
        }

        artifacts.Add(new TemporaryDownloadArtifact(
            fullPath,
            GetCandidateFinalNameFromTemporaryPath(fullPath),
            nowUtc));
    }

    private bool TryConsumeRecentTemporaryArtifactLocked(string targetFullPath)
    {
        var directory = Path.GetDirectoryName(targetFullPath);
        var targetName = Path.GetFileName(targetFullPath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(targetName))
        {
            return false;
        }

        if (!_temporaryArtifactsByDirectory.TryGetValue(directory, out var artifacts) || artifacts.Count == 0)
        {
            return false;
        }

        var strongMatch = artifacts.FirstOrDefault(artifact =>
            IsLikelySameDownloadTarget(artifact.CandidateFinalName, targetName));

        if (strongMatch.FullPath != null)
        {
            artifacts.Remove(strongMatch);
            if (artifacts.Count == 0)
            {
                _temporaryArtifactsByDirectory.Remove(directory);
            }

            return true;
        }

        return false;
    }

    private bool ShouldSuppressDueToRecentResolvedTargetLocked(string fullPath, DateTime nowUtc)
    {
        if (!_recentResolvedTargets.TryGetValue(fullPath, out var resolvedTarget))
        {
            return false;
        }

        return nowUtc - resolvedTarget.TimestampUtc <= PostResolutionSuppressionWindow;
    }

    private void TrackResolvedTargetLocked(string fullPath, FileEventType eventType, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return;
        }

        _recentResolvedTargets[fullPath] = new ResolvedTarget(eventType, nowUtc);
    }

    private void CleanupState(DateTime nowUtc)
    {
        CleanupDeletedCandidates(nowUtc);
        CleanupTemporaryArtifacts(nowUtc);
        CleanupResolvedTargets(nowUtc);
    }

    private void CleanupDeletedCandidates(DateTime nowUtc)
    {
        var staleThreshold = nowUtc - MoveCandidateWindow;
        var keysToRemove = new List<string>();

        foreach (var pair in _deletedCandidatesByFileName)
        {
            pair.Value.RemoveAll(candidate => candidate.TimestampUtc < staleThreshold);
            if (pair.Value.Count == 0)
            {
                keysToRemove.Add(pair.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _deletedCandidatesByFileName.Remove(key);
        }
    }

    private void CleanupTemporaryArtifacts(DateTime nowUtc)
    {
        var staleThreshold = nowUtc - TemporaryDownloadWindow;
        var keysToRemove = new List<string>();

        foreach (var pair in _temporaryArtifactsByDirectory)
        {
            pair.Value.RemoveAll(artifact => artifact.TimestampUtc < staleThreshold);
            if (pair.Value.Count == 0)
            {
                keysToRemove.Add(pair.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _temporaryArtifactsByDirectory.Remove(key);
        }
    }

    private void CleanupResolvedTargets(DateTime nowUtc)
    {
        var staleThreshold = nowUtc - PostResolutionSuppressionWindow;
        var keysToRemove = _recentResolvedTargets
            .Where(pair => pair.Value.TimestampUtc < staleThreshold)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _recentResolvedTargets.Remove(key);
        }
    }

    private static string GetCandidateFinalNameFromTemporaryPath(string temporaryPath)
    {
        var fileName = Path.GetFileName(temporaryPath);
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(extension))
        {
            return fileName;
        }

        if (!DownloadTemporaryExtensions.Contains(extension))
        {
            return fileName;
        }

        return Path.GetFileNameWithoutExtension(fileName);
    }

    private static bool IsLikelySameDownloadTarget(string temporaryCandidateFileName, string targetFileName)
    {
        if (string.IsNullOrWhiteSpace(temporaryCandidateFileName) || string.IsNullOrWhiteSpace(targetFileName))
        {
            return false;
        }

        if (temporaryCandidateFileName.Equals(targetFileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return temporaryCandidateFileName.StartsWith(targetFileName, StringComparison.OrdinalIgnoreCase)
               || targetFileName.StartsWith(temporaryCandidateFileName, StringComparison.OrdinalIgnoreCase);
    }

    private readonly struct DeletedCandidate
    {
        public DeletedCandidate(string fullPath, DateTime timestampUtc)
        {
            FullPath = fullPath;
            TimestampUtc = timestampUtc;
        }

        public string FullPath { get; }

        public DateTime TimestampUtc { get; }
    }

    private readonly struct TemporaryDownloadArtifact
    {
        public TemporaryDownloadArtifact(string fullPath, string candidateFinalName, DateTime timestampUtc)
        {
            FullPath = fullPath;
            CandidateFinalName = candidateFinalName;
            TimestampUtc = timestampUtc;
        }

        public string FullPath { get; }

        public string CandidateFinalName { get; }

        public DateTime TimestampUtc { get; }
    }

    private readonly struct ResolvedTarget
    {
        public ResolvedTarget(FileEventType eventType, DateTime timestampUtc)
        {
            EventType = eventType;
            TimestampUtc = timestampUtc;
        }

        public FileEventType EventType { get; }

        public DateTime TimestampUtc { get; }
    }
}
