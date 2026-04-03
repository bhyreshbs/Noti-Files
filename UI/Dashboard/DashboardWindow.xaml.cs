using System;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using WinRT;
using WinRT.Interop;
using FileActivityNotifier.Core;
using FileActivityNotifier.Models;
using FileActivityNotifier.Services;
using FileActivityNotifier.UI.Dashboard.Pages;

namespace FileActivityNotifier.UI.Dashboard;

public partial class DashboardWindow : Window
{
    private bool _enableTrayBehavior;
    private bool _allowApplicationExit;
    private bool _isTrayHidden;
    private bool _isSyncingToggle;
    private bool _previewModeEnabled = true;
    private readonly App _app;
    private readonly DashboardViewModel _viewModel;
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;
    private readonly FileWatcherService _fileWatcherService;
    private readonly AppWindow _appWindow;
    private readonly OverlappedPresenter _presenter;
    private MicaController? _micaController;
    private SystemBackdropConfiguration? _backdropConfiguration;

    public DashboardWindow(SettingsService settingsService, StartupService startupService, FileWatcherService fileWatcherService)
    {
        InitializeComponent();
        _app = (App)Application.Current;
        _enableTrayBehavior = false;
        _allowApplicationExit = false;
        _isTrayHidden = false;
        _settingsService = settingsService;
        _startupService = startupService;
        _fileWatcherService = fileWatcherService;

        _viewModel = new DashboardViewModel();
        _viewModel.IsMonitoring = _settingsService.Current.IsMonitoringEnabled;
        DashboardRoot.DataContext = _viewModel;

        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        _presenter = OverlappedPresenter.Create();
        _presenter.IsResizable = true;
        _presenter.IsMaximizable = true;
        _presenter.IsMinimizable = true;
        _appWindow.SetPresenter(_presenter);
        _appWindow.IsShownInSwitchers = true;
        _appWindow.Closing += AppWindow_Closing;
        _appWindow.Changed += AppWindow_Changed;
        TrySetWindowIcon();

        CenterWindow(windowId, 900, 600);
        TryApplyMicaBackdrop();

        SetMonitoringUi(_viewModel.IsMonitoring);

        DashboardRoot.Loaded += DashboardWindow_Loaded;
        Activated += DashboardWindow_Activated;
        Closed += DashboardWindow_Closed;
    }

    public bool IsTrayHidden => _isTrayHidden;

    public bool IsBackgroundMode
    {
        get
        {
            if (_isTrayHidden)
            {
                return true;
            }

            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                return presenter.State == OverlappedPresenterState.Minimized;
            }

            return false;
        }
    }

    public void SetTrayBehavior(bool enabled)
    {
        _enableTrayBehavior = enabled;
    }

    public void SetPreviewMode(bool enabled)
    {
        _previewModeEnabled = enabled;
    }

    public void AllowApplicationExit()
    {
        _allowApplicationExit = true;
    }

    public void HideToTray()
    {
        _appWindow.Hide();
        _isTrayHidden = true;
    }

    public void AddFileEvent(FileEvent fileEvent)
    {
        if (fileEvent == null)
        {
            return;
        }

        var item = new DashboardEventItem
        {
            FileName = fileEvent.FileName,
            FilePath = fileEvent.FilePath,
            FileExtension = fileEvent.Extension,
            EventType = fileEvent.EventType.ToString(),
            Timestamp = fileEvent.Timestamp.Kind == System.DateTimeKind.Utc
                ? fileEvent.Timestamp.ToLocalTime()
                : fileEvent.Timestamp
        };

        _viewModel.AddEvent(item);
    }

    public static void ShowOrFocus(DashboardWindow? dashboardWindow)
    {
        if (dashboardWindow is null)
        {
            return;
        }

        dashboardWindow._appWindow.Show();
        dashboardWindow._isTrayHidden = false;

        if (dashboardWindow._appWindow.Presenter is OverlappedPresenter presenter
            && presenter.State == OverlappedPresenterState.Minimized)
        {
            presenter.Restore();
        }

        dashboardWindow.Activate();
    }

    public void NavigateToActivityPage()
    {
        ContentFrame.Content = new ActivityPage(_viewModel);
        SetDashboardChromeVisibility(showDashboardChrome: true);
        RootNavView.SelectedItem = ActivityNavItem;
    }

    public void NavigateToSettingsPage()
    {
        ContentFrame.Content = new SettingsPage(_settingsService, _startupService, _fileWatcherService);
        SetDashboardChromeVisibility(showDashboardChrome: false);
        RootNavView.SelectedItem = SettingsNavItem;
    }

    private void DashboardWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_previewModeEnabled)
        {
            SeedPreviewEventsIfEmpty();
        }

        NavigateToActivityPage();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var selectedItem = args.SelectedItem as NavigationViewItem;
        var tag = selectedItem?.Tag?.ToString();
        if (string.Equals(tag, "settings", StringComparison.OrdinalIgnoreCase))
        {
            NavigateToSettingsPage();
            return;
        }

        NavigateToActivityPage();
    }

    private void PauseResumeToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_isSyncingToggle)
        {
            return;
        }

        SetMonitoringState(false);
    }

    private void PauseResumeToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isSyncingToggle)
        {
            return;
        }

        SetMonitoringState(true);
    }

    private void SetMonitoringState(bool isMonitoring)
    {
        _viewModel.IsMonitoring = isMonitoring;

        if (_viewModel.IsMonitoring)
        {
            _fileWatcherService.Resume();
        }
        else
        {
            _fileWatcherService.Pause();
        }

        _settingsService.Current.IsMonitoringEnabled = _viewModel.IsMonitoring;
        _settingsService.Save();

        SetMonitoringUi(_viewModel.IsMonitoring);
    }

    private void SetMonitoringUi(bool isMonitoring)
    {
        _isSyncingToggle = true;
        PauseResumeToggle.IsChecked = !isMonitoring;
        _isSyncingToggle = false;

        PauseResumeIcon.Glyph = isMonitoring ? "\uE769" : "\uE768";
        PauseResumeLabel.Text = isMonitoring ? "Pause" : "Resume";
        MonitoringInfoBar.Severity = isMonitoring ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        MonitoringInfoBar.Title = isMonitoring ? "Monitoring Active" : "Monitoring Paused";
        MonitoringInfoBar.Message = isMonitoring
            ? "File activity watcher is running."
            : "Watcher is paused. Resume to continue receiving events.";
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_allowApplicationExit)
        {
            args.Cancel = true;
            HideToTray();
        }
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!_enableTrayBehavior)
        {
            return;
        }

        if (sender.Presenter is OverlappedPresenter presenter
            && presenter.State == OverlappedPresenterState.Minimized)
        {
            sender.Hide();
            _isTrayHidden = true;
        }
    }

    private void DashboardWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_backdropConfiguration != null)
        {
            _backdropConfiguration.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }
    }

    private void DashboardWindow_Closed(object sender, WindowEventArgs args)
    {
        _micaController?.Dispose();
        _micaController = null;
    }

    private void CenterWindow(WindowId windowId, int width, int height)
    {
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;
        var x = workArea.X + Math.Max(0, (workArea.Width - width) / 2);
        var y = workArea.Y + Math.Max(0, (workArea.Height - height) / 2);
        _appWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    private void TryApplyMicaBackdrop()
    {
        if (!MicaController.IsSupported())
        {
            return;
        }

        _backdropConfiguration = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = _app.CurrentTheme == ElementTheme.Light
                ? SystemBackdropTheme.Light
                : SystemBackdropTheme.Dark
        };

        _micaController = new MicaController
        {
            Kind = MicaKind.BaseAlt
        };

        _micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        _micaController.SetSystemBackdropConfiguration(_backdropConfiguration);
    }

    private void SeedPreviewEventsIfEmpty()
    {
        if (_viewModel.AllEvents.Count > 0)
        {
            return;
        }

        _viewModel.AddEvent(new DashboardEventItem
        {
            FileName = "Quarterly_Report_Q1.pdf",
            FilePath = @"C:\Users\harsh\Documents\Quarterly_Report_Q1.pdf",
            FileExtension = ".pdf",
            EventType = "Saved",
            Timestamp = System.DateTime.Now.AddMinutes(-3)
        });

        _viewModel.AddEvent(new DashboardEventItem
        {
            FileName = "Product_Roadmap.xlsx",
            FilePath = @"C:\Users\harsh\Downloads\Product_Roadmap.xlsx",
            FileExtension = ".xlsx",
            EventType = "Downloaded",
            Timestamp = System.DateTime.Now.AddMinutes(-12)
        });

        _viewModel.AddEvent(new DashboardEventItem
        {
            FileName = "Holiday_Photo.jpg",
            FilePath = @"C:\Users\harsh\Desktop\Photos\Holiday_Photo.jpg",
            FileExtension = ".jpg",
            EventType = "Moved",
            Timestamp = System.DateTime.Now.AddHours(-1)
        });
    }

    private void SetDashboardChromeVisibility(bool showDashboardChrome)
    {
        RootNavView.AlwaysShowHeader = showDashboardChrome;
        DashboardHeader.Visibility = showDashboardChrome ? Visibility.Visible : Visibility.Collapsed;
        MonitoringInfoBar.Visibility = showDashboardChrome ? Visibility.Visible : Visibility.Collapsed;
        ContentTopSpacerRow.Height = showDashboardChrome ? new GridLength(10) : new GridLength(0);
    }

    private void PauseResumeToggle_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        PauseResumeToggle.Opacity = 0.96;
    }

    private void PauseResumeToggle_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        PauseResumeToggle.Opacity = 1.0;
    }

    private void PauseResumeToggle_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        PauseResumeToggle.Opacity = 0.88;
    }

    private void PauseResumeToggle_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        PauseResumeToggle.Opacity = PauseResumeToggle.IsPointerOver ? 0.96 : 1.0;
    }

    private void TrySetWindowIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Icons", "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                _appWindow.SetIcon(iconPath);
            }
        }
        catch
        {
            // Icon fallback is handled by the platform.
        }
    }
}
