using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Microsoft.UI.Xaml;
using Drawing = System.Drawing;
using WF = System.Windows.Forms;
using FileActivityNotifier.Core;
using FileActivityNotifier.UI.Dashboard;

namespace FileActivityNotifier.Services;

public class TrayIconService : IDisposable
{
    private WF.NotifyIcon? _notifyIcon;
    private DashboardWindow? _dashboardWindow;
    private FileWatcherService? _fileWatcherService;
    private WF.ToolStripMenuItem? _pauseResumeMenuItem;
    private WF.ContextMenuStrip? _contextMenu;

    public void Initialize(DashboardWindow dashboardWindow, FileWatcherService fileWatcherService)
    {
        _dashboardWindow = dashboardWindow;
        _fileWatcherService = fileWatcherService;

        _contextMenu = new WF.ContextMenuStrip();

        var openDashboardMenuItem = new WF.ToolStripMenuItem("Open Dashboard");
        openDashboardMenuItem.Click += (_, _) => OpenDashboard();

        _pauseResumeMenuItem = new WF.ToolStripMenuItem("Pause");
        _pauseResumeMenuItem.Click += (_, _) => ToggleMonitoring();

        var exitMenuItem = new WF.ToolStripMenuItem("Exit");
        exitMenuItem.Click += (_, _) => ExitApplication();

        _contextMenu.Items.Add(openDashboardMenuItem);
        _contextMenu.Items.Add(_pauseResumeMenuItem);
        _contextMenu.Items.Add(exitMenuItem);

        _notifyIcon = new WF.NotifyIcon
        {
            Text = "Noti-Files",
            Icon = ResolveIcon(),
            Visible = true,
            ContextMenuStrip = _contextMenu
        };

        _notifyIcon.DoubleClick += (_, _) => OpenDashboard();
        UpdatePauseResumeLabel();
    }

    public void Dispose()
    {
        if (_notifyIcon == null)
        {
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;

        _contextMenu?.Dispose();
        _contextMenu = null;
    }

    public void ToggleMonitoring()
    {
        if (_fileWatcherService == null)
        {
            return;
        }

        if (_fileWatcherService.IsPaused)
        {
            _fileWatcherService.Resume();
        }
        else
        {
            _fileWatcherService.Pause();
        }

        UpdatePauseResumeLabel();
    }

    public void UpdatePauseResumeLabel()
    {
        if (_pauseResumeMenuItem == null || _fileWatcherService == null)
        {
            return;
        }

        _pauseResumeMenuItem.Text = _fileWatcherService.IsPaused ? "Resume" : "Pause";
    }

    public void OpenDashboard()
    {
        if (_dashboardWindow == null)
        {
            return;
        }

        _dashboardWindow.DispatcherQueue.TryEnqueue(() => DashboardWindow.ShowOrFocus(_dashboardWindow));
    }

    public void ExitApplication()
    {
        if (_dashboardWindow != null)
        {
            _dashboardWindow.AllowApplicationExit();
            _dashboardWindow.SetTrayBehavior(false);
            _dashboardWindow.Close();
        }

        if (Application.Current is App app)
        {
            app.Cleanup();
        }

        Application.Current?.Exit();
    }

    private static Icon ResolveIcon()
    {
        try
        {
            var packagedIconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Icons", "AppIcon.ico");
            if (File.Exists(packagedIconPath))
            {
                return new Icon(packagedIconPath);
            }

            var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                return Drawing.Icon.ExtractAssociatedIcon(executablePath) ?? Drawing.SystemIcons.Application;
            }
        }
        catch
        {
            // Fallback icon used below.
        }

        return Drawing.SystemIcons.Application;
    }
}
