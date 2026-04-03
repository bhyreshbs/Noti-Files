using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using WF = System.Windows.Forms;
using FileActivityNotifier.Core;
using FileActivityNotifier.Models;
using FileActivityNotifier.Services;
using FileActivityNotifier.UI.Dashboard;
using FileActivityNotifier.UI.Toast;

namespace FileActivityNotifier;

public partial class App : Application
{
    private const string AppDisplayName = "NotiFiles";
    private const string ShortcutFileName = "NotiFiles.lnk";
    private const string PreferredInstallFolderName = "Noti-Files";
    private const string FallbackInstallFolderName = "NotiFiles";
    private const string SetupStateFileName = "install-setup.state";

    private static readonly string DiagnosticLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NotiFiles",
        "diagnostics.log");

    private ElementTheme _currentTheme = ElementTheme.Dark;

    internal static DispatcherQueue? UiDispatcherQueue { get; private set; }

    private SettingsService? _settingsService;
    private StartupService? _startupService;
    private FileWatcherService? _fileWatcherService;
    private NotificationManager? _notificationManager;
    private TrayIconService? _trayIconService;
    private DashboardWindow? _dashboard;
    private AppInstance? _mainAppInstance;

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        UiDispatcherQueue = DispatcherQueue.GetForCurrentThread();

        InitializeDiagnostics();
        LogDiagnostic("OnLaunched entered.");

        if (!await EnsureSingleInstanceAsync())
        {
            LogDiagnostic("Secondary instance redirected activation and will exit.");
            Current.Exit();
            return;
        }

        SetupAppEnvironment();

        try
        {
            LogDiagnostic("Creating services.");
            _settingsService = new SettingsService();
            _startupService = new StartupService();
            var settings = _settingsService.Load();

            if (settings.IsMonitoringEnabled)
            {
                settings.IsMonitoringEnabled = false;
                _settingsService.Save(settings);
                LogDiagnostic("Monitoring auto-disabled at launch for stability fallback.");
            }

            LogDiagnostic($"Settings loaded. WatchedFolders={settings.WatchedFolders.Count}, Monitoring={settings.IsMonitoringEnabled}");

            SetAppTheme(settings.Theme);

            _notificationManager = new NotificationManager(new ToastStackManager());
            _fileWatcherService = new FileWatcherService(
                _settingsService,
                new EventProcessor(),
                new FilterEngine());
            LogDiagnostic("Notification and watcher services created.");

            _dashboard = new DashboardWindow(_settingsService, _startupService, _fileWatcherService);
            _dashboard.SetPreviewMode(false);
            ApplyThemeToWindow(_dashboard);
            LogDiagnostic("Dashboard window created.");

            LoadDashboardHistory();
            LogDiagnostic("Dashboard history loaded.");

            _fileWatcherService.FileEventDetected += FileWatcherService_FileEventDetected;
            LogDiagnostic("Watcher event subscription completed.");

            if (settings.IsMonitoringEnabled)
            {
                _fileWatcherService.Start();
                LogDiagnostic("Watcher started.");
            }

            _trayIconService = new TrayIconService();
            _trayIconService.Initialize(_dashboard, _fileWatcherService);
            LogDiagnostic("Tray icon initialized.");
            _dashboard.SetTrayBehavior(true);
            _dashboard.Activate();
            LogDiagnostic("Startup complete (dashboard visible).");
        }
        catch (Exception ex)
        {
            LogDiagnostic($"Startup exception: {ex}");
            WF.MessageBox.Show(ex.ToString(), "Startup Error", WF.MessageBoxButtons.OK, WF.MessageBoxIcon.Error);
            Current.Exit();
        }
    }

    private void FileWatcherService_FileEventDetected(object? sender, FileEvent fileEvent)
    {
        if (_settingsService == null)
        {
            return;
        }

        void ProcessOnUiThread()
        {
            // Temporary stability guard: custom WinUI toast windows are currently crashing in native XAML.
            var showToast = false;
            _notificationManager?.Show(fileEvent, _settingsService.Current, showToast);
            _dashboard?.AddFileEvent(fileEvent);
        }

        if (_dashboard?.DispatcherQueue != null)
        {
            _dashboard.DispatcherQueue.TryEnqueue(ProcessOnUiThread);
            return;
        }

        if (UiDispatcherQueue != null)
        {
            UiDispatcherQueue.TryEnqueue(ProcessOnUiThread);
            return;
        }

        ProcessOnUiThread();
    }

    private void LoadDashboardHistory()
    {
        if (_dashboard == null || _notificationManager == null)
        {
            return;
        }

        var snapshot = _notificationManager.SnapshotLog();
        if (snapshot.Events.Count == 0)
        {
            return;
        }

        // Add oldest-to-newest so newest stays at top after dashboard insertion logic.
        for (var index = snapshot.Events.Count - 1; index >= 0; index--)
        {
            _dashboard.AddFileEvent(snapshot.Events[index]);
        }
    }

    internal ElementTheme CurrentTheme => _currentTheme;

    internal void ApplyThemeToWindow(Window? window)
    {
        if (window?.Content is FrameworkElement root)
        {
            root.RequestedTheme = _currentTheme;
        }
    }

    internal void SetAppTheme(string theme)
    {
        _currentTheme = theme.Equals("Light", StringComparison.OrdinalIgnoreCase)
            ? ElementTheme.Light
            : ElementTheme.Dark;

        ApplyThemeToWindow(_dashboard);
    }

    internal void Cleanup()
    {
        LogDiagnostic("Cleanup entered.");

        if (_mainAppInstance != null)
        {
            _mainAppInstance.Activated -= MainAppInstance_Activated;
            _mainAppInstance = null;
        }

        if (_fileWatcherService != null)
        {
            _fileWatcherService.FileEventDetected -= FileWatcherService_FileEventDetected;
            _fileWatcherService.Stop();
            _fileWatcherService.Dispose();
        }

        _trayIconService?.Dispose();
        _settingsService?.Save();
        LogDiagnostic("Cleanup completed.");
    }

    private async Task<bool> EnsureSingleInstanceAsync()
    {
        try
        {
            var current = AppInstance.GetCurrent();
            var mainInstance = AppInstance.FindOrRegisterForKey("FileActivityNotifier.Main");

            if (!mainInstance.IsCurrent)
            {
                await mainInstance.RedirectActivationToAsync(current.GetActivatedEventArgs());
                return false;
            }

            _mainAppInstance = mainInstance;
            _mainAppInstance.Activated += MainAppInstance_Activated;
            return true;
        }
        catch (Exception ex)
        {
            LogDiagnostic($"Single-instance check failed; continuing without redirection: {ex}");
            return true;
        }
    }

    private void MainAppInstance_Activated(object? sender, AppActivationArguments args)
    {
        UiDispatcherQueue?.TryEnqueue(() =>
        {
            if (_dashboard == null)
            {
                return;
            }

            DashboardWindow.ShowOrFocus(_dashboard);
            _dashboard.NavigateToActivityPage();
        });
    }

    private static void InitializeDiagnostics()
    {
        try
        {
            var directory = Path.GetDirectoryName(DiagnosticLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            LogDiagnostic("==== App startup session ====");

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                if (Current is App app)
                {
                    app.Cleanup();
                }
            };

            Current.UnhandledException += (_, args) =>
            {
                LogDiagnostic($"DispatcherUnhandledException: {args.Exception}");
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                LogDiagnostic($"AppDomain.UnhandledException (Terminating={args.IsTerminating}): {args.ExceptionObject}");
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                LogDiagnostic($"TaskScheduler.UnobservedTaskException: {args.Exception}");
            };
        }
        catch
        {
            // Diagnostics must never crash app startup.
        }
    }

    private static void LogDiagnostic(string message)
    {
        try
        {
            File.AppendAllText(
                DiagnosticLogPath,
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}{Environment.NewLine}");
        }
        catch
        {
            // Ignore logging failures.
        }
    }

    private static void SetupAppEnvironment()
    {
        try
        {
            var currentExecutablePath = ResolveApplicationExecutablePath();
            if (string.IsNullOrWhiteSpace(currentExecutablePath) || !File.Exists(currentExecutablePath))
            {
                LogDiagnostic("SetupAppEnvironment skipped: executable path not resolved.");
                return;
            }

            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var setupStateDirectory = Path.Combine(localAppDataPath, FallbackInstallFolderName);
            var setupStatePath = Path.Combine(setupStateDirectory, SetupStateFileName);

            var sourceDirectory = Path.GetDirectoryName(currentExecutablePath) ?? AppContext.BaseDirectory;
            var executableName = Path.GetFileName(currentExecutablePath);

            var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var preferredInstallDirectory = Path.Combine(programFilesPath, PreferredInstallFolderName);
            var fallbackInstallDirectory = Path.Combine(localAppDataPath, FallbackInstallFolderName);
            var hasSetupState = TryReadSetupState(setupStatePath, out var installedExeFromState);

            var installedExecutablePath = string.Empty;
            var installedWorkingDirectory = string.Empty;

            if (hasSetupState
                && File.Exists(installedExeFromState)
                && TryInstallToDirectory(
                    sourceDirectory,
                    Path.GetDirectoryName(installedExeFromState) ?? fallbackInstallDirectory,
                    executableName,
                    out installedExecutablePath,
                    out installedWorkingDirectory))
            {
                LogDiagnostic($"SetupAppEnvironment synchronized existing install directory: {installedWorkingDirectory}");
            }
            else if (TryInstallToDirectory(sourceDirectory, preferredInstallDirectory, executableName, out installedExecutablePath, out installedWorkingDirectory))
            {
                LogDiagnostic($"SetupAppEnvironment installed in preferred directory: {preferredInstallDirectory}");
            }
            else if (TryInstallToDirectory(sourceDirectory, fallbackInstallDirectory, executableName, out installedExecutablePath, out installedWorkingDirectory))
            {
                LogDiagnostic($"SetupAppEnvironment installed in fallback directory: {fallbackInstallDirectory}");
            }
            else
            {
                LogDiagnostic("SetupAppEnvironment failed: both preferred and fallback install directories failed.");
                return;
            }

            CreateDesktopShortcutIfMissing(installedExecutablePath, installedWorkingDirectory);
            CreateStartMenuShortcutIfMissing(installedExecutablePath, installedWorkingDirectory);

            SaveSetupState(setupStateDirectory, setupStatePath, installedExecutablePath);
            LogDiagnostic("SetupAppEnvironment completed successfully.");
        }
        catch (Exception ex)
        {
            LogDiagnostic($"SetupAppEnvironment failed: {ex}");
        }
    }

    private static bool TryInstallToDirectory(
        string sourceDirectory,
        string destinationDirectory,
        string executableName,
        out string installedExecutablePath,
        out string installedWorkingDirectory)
    {
        installedExecutablePath = string.Empty;
        installedWorkingDirectory = string.Empty;

        try
        {
            Directory.CreateDirectory(destinationDirectory);

            if (!string.Equals(sourceDirectory, destinationDirectory, StringComparison.OrdinalIgnoreCase))
            {
                SyncDirectoryFiles(sourceDirectory, destinationDirectory);
            }

            var candidateExecutable = Path.Combine(destinationDirectory, executableName);
            if (!File.Exists(candidateExecutable))
            {
                LogDiagnostic($"SetupAppEnvironment failed for {destinationDirectory}: installed executable missing after copy.");
                return false;
            }

            installedExecutablePath = candidateExecutable;
            installedWorkingDirectory = destinationDirectory;
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            LogDiagnostic($"SetupAppEnvironment permission issue for {destinationDirectory}: {ex.Message}");
            return false;
        }
        catch (IOException ex)
        {
            LogDiagnostic($"SetupAppEnvironment IO issue for {destinationDirectory}: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            LogDiagnostic($"SetupAppEnvironment error for {destinationDirectory}: {ex.Message}");
            return false;
        }
    }

    private static void SyncDirectoryFiles(string sourceDirectory, string destinationDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            if (!Directory.Exists(destinationPath))
            {
                Directory.CreateDirectory(destinationPath);
            }
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var destinationFilePath = Path.Combine(destinationDirectory, relativePath);
            var sourceInfo = new FileInfo(file);

            var shouldCopy = true;
            if (File.Exists(destinationFilePath))
            {
                var destinationInfo = new FileInfo(destinationFilePath);
                shouldCopy = sourceInfo.Length != destinationInfo.Length
                             || sourceInfo.LastWriteTimeUtc > destinationInfo.LastWriteTimeUtc;
            }

            if (!shouldCopy)
            {
                continue;
            }

            var destinationFolder = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrWhiteSpace(destinationFolder) && !Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            File.Copy(file, destinationFilePath, overwrite: true);
        }
    }

    private static void CreateDesktopShortcutIfMissing(string targetExecutablePath, string workingDirectory)
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktopPath) || !Directory.Exists(desktopPath))
        {
            return;
        }

        var shortcutPath = Path.Combine(desktopPath, ShortcutFileName);
        CreateShortcutIfMissing(shortcutPath, targetExecutablePath, workingDirectory);
    }

    private static void CreateStartMenuShortcutIfMissing(string targetExecutablePath, string workingDirectory)
    {
        var programsPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        if (string.IsNullOrWhiteSpace(programsPath) || !Directory.Exists(programsPath))
        {
            return;
        }

        var appProgramsDirectory = Path.Combine(programsPath, AppDisplayName);
        Directory.CreateDirectory(appProgramsDirectory);

        var shortcutPath = Path.Combine(appProgramsDirectory, ShortcutFileName);
        CreateShortcutIfMissing(shortcutPath, targetExecutablePath, workingDirectory);
    }

    private static void CreateShortcutIfMissing(string shortcutPath, string targetExecutablePath, string workingDirectory)
    {
        var alreadyExists = File.Exists(shortcutPath);

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
        {
            LogDiagnostic($"Shortcut skipped (WScript.Shell unavailable): {shortcutPath}");
            return;
        }

        var shell = Activator.CreateInstance(shellType);
        if (shell == null)
        {
            LogDiagnostic($"Shortcut skipped (cannot create shell instance): {shortcutPath}");
            return;
        }

        try
        {
            var shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                new object[] { shortcutPath });

            if (shortcut == null)
            {
                LogDiagnostic($"Shortcut skipped (shortcut object null): {shortcutPath}");
                return;
            }

            try
            {
                var shortcutType = shortcut.GetType();

                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetExecutablePath });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDirectory });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { AppDisplayName });

                if (File.Exists(targetExecutablePath))
                {
                    shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { $"{targetExecutablePath},0" });
                }

                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);

                LogDiagnostic($"Shortcut {(alreadyExists ? "updated" : "created")}: {shortcutPath}");
            }
            finally
            {
                if (Marshal.IsComObject(shortcut))
                {
                    Marshal.FinalReleaseComObject(shortcut);
                }
            }
        }
        finally
        {
            if (Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    private static bool TryReadSetupState(string setupStatePath, out string installedExecutablePath)
    {
        installedExecutablePath = string.Empty;

        try
        {
            if (!File.Exists(setupStatePath))
            {
                return false;
            }

            var content = File.ReadAllText(setupStatePath).Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            installedExecutablePath = content;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SaveSetupState(string setupStateDirectory, string setupStatePath, string installedExecutablePath)
    {
        Directory.CreateDirectory(setupStateDirectory);
        File.WriteAllText(setupStatePath, installedExecutablePath);
    }

    private static string ResolveApplicationExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath)
            && File.Exists(processPath)
            && !string.Equals(Path.GetFileName(processPath), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        var entryAssemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (!string.IsNullOrWhiteSpace(entryAssemblyName))
        {
            var candidate = Path.Combine(AppContext.BaseDirectory, $"{entryAssemblyName}.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return !string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath)
            ? processPath
            : string.Empty;
    }
}
