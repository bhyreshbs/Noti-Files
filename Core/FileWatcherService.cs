using System;
using System.Collections.Generic;
using System.IO;
using FileActivityNotifier.Models;
using FileActivityNotifier.Services;

namespace FileActivityNotifier.Core;

public class FileWatcherService : IDisposable
{
    private readonly object _sync = new();
    private readonly List<FileSystemWatcher> _watchers = new();

    private readonly SettingsService _settingsService;
    private readonly EventProcessor _eventProcessor;
    private readonly FilterEngine _filterEngine;

    private bool _isDisposed;

    public bool IsRunning { get; private set; }

    public bool IsPaused { get; private set; }

    public event EventHandler<FileEvent>? FileEventDetected;

    public FileWatcherService(SettingsService settingsService, EventProcessor eventProcessor, FilterEngine filterEngine)
    {
        _settingsService = settingsService;
        _eventProcessor = eventProcessor;
        _filterEngine = filterEngine;
    }

    public void Start()
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            if (IsRunning)
            {
                return;
            }

            var settings = _settingsService.Load();
            CreateWatchers(settings.WatchedFolders);
            IsPaused = false;
            IsRunning = true;
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (!IsRunning)
            {
                return;
            }

            DisposeWatchers();
            IsRunning = false;
            IsPaused = false;
        }
    }

    public void Pause()
    {
        lock (_sync)
        {
            IsPaused = true;
        }
    }

    public void Resume()
    {
        lock (_sync)
        {
            IsPaused = false;
        }
    }

    public void Restart()
    {
        lock (_sync)
        {
            if (!IsRunning)
            {
                return;
            }

            var wasPaused = IsPaused;
            var settings = _settingsService.Load();
            CreateWatchers(settings.WatchedFolders);
            IsPaused = wasPaused;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Stop();
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    private void CreateWatchers(List<string> watchedFolders)
    {
        DisposeWatchers();

        foreach (var folder in watchedFolders)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                continue;
            }

            var watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = true,
                Filter = "*.*",
                NotifyFilter = NotifyFilters.FileName
                               | NotifyFilters.DirectoryName
                               | NotifyFilters.CreationTime
                               | NotifyFilters.LastWrite
                               | NotifyFilters.Size
            };

            watcher.Created += Watcher_Created;
            watcher.Changed += Watcher_Changed;
            watcher.Deleted += Watcher_Deleted;
            watcher.Renamed += Watcher_Renamed;
            watcher.Error += Watcher_Error;
            watcher.EnableRaisingEvents = true;

            _watchers.Add(watcher);
        }
    }

    private void DisposeWatchers()
    {
        foreach (var watcher in _watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Created -= Watcher_Created;
                watcher.Changed -= Watcher_Changed;
                watcher.Deleted -= Watcher_Deleted;
                watcher.Renamed -= Watcher_Renamed;
                watcher.Error -= Watcher_Error;
                watcher.Dispose();
            }
            catch
            {
                // Keep disposal resilient.
            }
        }

        _watchers.Clear();
    }

    private void Watcher_Created(object sender, FileSystemEventArgs e)
    {
        PublishIfEligible(_eventProcessor.Process(e));
    }

    private void Watcher_Changed(object sender, FileSystemEventArgs e)
    {
        PublishIfEligible(_eventProcessor.Process(e));
    }

    private void Watcher_Deleted(object sender, FileSystemEventArgs e)
    {
        PublishIfEligible(_eventProcessor.Process(e));
    }

    private void Watcher_Renamed(object sender, RenamedEventArgs e)
    {
        PublishIfEligible(_eventProcessor.Process(e));
    }

    private void Watcher_Error(object sender, ErrorEventArgs e)
    {
        lock (_sync)
        {
            if (!IsRunning || _isDisposed)
            {
                return;
            }

            var settings = _settingsService.Load();
            CreateWatchers(settings.WatchedFolders);
        }
    }

    private void PublishIfEligible(FileEvent? fileEvent)
    {
        if (fileEvent == null)
        {
            return;
        }

        AppSettings settings;
        lock (_sync)
        {
            if (!IsRunning || IsPaused)
            {
                return;
            }

            settings = _settingsService.Current;
        }

        if (_filterEngine.ShouldShow(fileEvent, settings))
        {
            FileEventDetected?.Invoke(this, fileEvent);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(FileWatcherService));
        }
    }
}
