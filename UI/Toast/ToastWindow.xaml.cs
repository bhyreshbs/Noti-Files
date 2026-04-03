using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using WinRT.Interop;

namespace FileActivityNotifier.UI.Toast;

public partial class ToastWindow : Window
{
    private const int ToastWidth = 372;
    private const int ToastBottomMargin = 26;
    private const int ToastRightMargin = 18;

    private readonly DispatcherQueueTimer _dismissTimer;
    private readonly DispatcherQueueTimer _timeAgoTimer;
    private readonly double _durationSeconds;
    private readonly AppWindow _appWindow;
    private readonly WindowId _windowId;
    private DateTime _dismissEndsAt;
    private bool _isDismissing;
    private int _stackIndex;

    public ToastViewModel ViewModel { get; }

    public event EventHandler? Dismissed;

    public ToastWindow(ToastViewModel viewModel, double durationSeconds = 5)
    {
        InitializeComponent();
        ViewModel = viewModel;
        ToastRoot.DataContext = ViewModel;

        var app = (App)Application.Current;
        app.ApplyThemeToWindow(this);

        _durationSeconds = Math.Max(0.5, durationSeconds);

        var hWnd = WindowNative.GetWindowHandle(this);
        _windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(_windowId);

        var presenter = OverlappedPresenter.Create();
        presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.SetBorderAndTitleBar(false, false);
        _appWindow.SetPresenter(presenter);
        _appWindow.IsShownInSwitchers = false;

        var dispatcherQueue = DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();

        _dismissTimer = dispatcherQueue.CreateTimer();
        _dismissTimer.Interval = TimeSpan.FromMilliseconds(40);
        _dismissTimer.Tick += DismissTimer_Tick;

        _timeAgoTimer = dispatcherQueue.CreateTimer();
        _timeAgoTimer.Interval = TimeSpan.FromSeconds(30);
        _timeAgoTimer.Tick += (_, _) => ViewModel.UpdateTimeAgo();

        ToastRoot.Loaded += ToastWindow_Loaded;
        Closed += ToastWindow_Closed;
    }

    public void ShowToastWindow()
    {
        _appWindow.Show();
        Activate();
    }

    public void SetStackIndex(int stackIndex)
    {
        _stackIndex = Math.Max(0, stackIndex);
        PositionToast(_stackIndex);
    }

    public void RepositionAnimated(int stackIndex)
    {
        _stackIndex = Math.Max(0, stackIndex);
        PositionToast(_stackIndex);
    }

    public void ForceDismiss()
    {
        DismissToast();
    }

    private void ToastWindow_Loaded(object sender, RoutedEventArgs e)
    {
        PositionToast(_stackIndex);
        StartEntryAnimation();

        _timeAgoTimer.Start();

        _dismissEndsAt = DateTime.Now.AddSeconds(_durationSeconds);
        _dismissTimer.Start();
    }

    private void PositionToast(int stackIndex)
    {
        var displayArea = DisplayArea.GetFromWindowId(_windowId, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        var height = (int)Math.Max(180, ToastRoot.ActualHeight);
        var x = workArea.X + workArea.Width - ToastWidth - ToastRightMargin;
        var y = workArea.Y + workArea.Height - height - ToastBottomMargin - (stackIndex * (height + 10));

        _appWindow.MoveAndResize(new RectInt32(x, y, ToastWidth, height));
    }

    private void StartEntryAnimation()
    {
        var visual = ElementCompositionPreview.GetElementVisual(ToastRoot);
        var compositor = visual.Compositor;

        visual.Opacity = 0f;
        visual.Offset = new Vector3(0f, 20f, 0f);

        var slideAnim = compositor.CreateScalarKeyFrameAnimation();
        slideAnim.InsertKeyFrame(0f, 20f);
        slideAnim.InsertKeyFrame(1f, 0f,
            compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.22f, 1f),
                new Vector2(0.36f, 1f)));
        slideAnim.Duration = TimeSpan.FromMilliseconds(300);
        slideAnim.Target = "Offset.Y";

        var fadeAnim = compositor.CreateScalarKeyFrameAnimation();
        fadeAnim.InsertKeyFrame(0f, 0f);
        fadeAnim.InsertKeyFrame(1f, 1f);
        fadeAnim.Duration = TimeSpan.FromMilliseconds(300);
        fadeAnim.Target = "Opacity";

        visual.StartAnimation("Offset.Y", slideAnim);
        visual.StartAnimation("Opacity", fadeAnim);
    }

    private void DismissTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        var remaining = (_dismissEndsAt - DateTime.Now).TotalMilliseconds;
        var total = TimeSpan.FromSeconds(_durationSeconds).TotalMilliseconds;

        var ratio = Math.Clamp(remaining / total, 0, 1);
        DismissProgressBar.Value = ratio * 100;

        if (remaining <= 0)
        {
            DismissToast();
        }
    }

    private void Toast_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _dismissTimer.Stop();
    }

    private void Toast_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        var remainingRatio = DismissProgressBar.Value / 100.0;
        var remainingMs = TimeSpan.FromSeconds(_durationSeconds).TotalMilliseconds * remainingRatio;
        _dismissEndsAt = DateTime.Now.AddMilliseconds(Math.Max(1, remainingMs));
        _dismissTimer.Start();
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(ViewModel.FilePath))
            {
                return;
            }

            Process.Start(new ProcessStartInfo(ViewModel.FilePath) { UseShellExecute = true });
        }
        catch
        {
            // Intentionally ignore to keep notifications non-blocking.
        }
    }

    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ViewModel.FilePath))
            {
                return;
            }

            if (File.Exists(ViewModel.FilePath))
            {
                Process.Start("explorer.exe", $"/select,\"{ViewModel.FilePath}\"");
                return;
            }

            var containingFolder = Path.GetDirectoryName(ViewModel.FilePath);
            if (!string.IsNullOrWhiteSpace(containingFolder) && Directory.Exists(containingFolder))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{containingFolder}\"") { UseShellExecute = true });
            }
        }
        catch
        {
            // Intentionally ignore to keep notifications non-blocking.
        }
    }

    private void Dismiss_Click(object sender, RoutedEventArgs e)
    {
        DismissToast();
    }

    private void ToastWindow_Closed(object sender, WindowEventArgs args)
    {
        _dismissTimer.Stop();
        _timeAgoTimer.Stop();
    }

    private void DismissToast()
    {
        if (_isDismissing)
        {
            return;
        }

        _isDismissing = true;
        _dismissTimer.Stop();
        _timeAgoTimer.Stop();

        var visual = ElementCompositionPreview.GetElementVisual(ToastRoot);
        var compositor = visual.Compositor;

        var slideAnim = compositor.CreateScalarKeyFrameAnimation();
        slideAnim.InsertKeyFrame(0f, 0f);
        slideAnim.InsertKeyFrame(1f, -16f,
            compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.22f, 1f),
                new Vector2(0.36f, 1f)));
        slideAnim.Duration = TimeSpan.FromMilliseconds(300);
        slideAnim.Target = "Offset.X";

        var fadeAnim = compositor.CreateScalarKeyFrameAnimation();
        fadeAnim.InsertKeyFrame(0f, 1f);
        fadeAnim.InsertKeyFrame(1f, 0f);
        fadeAnim.Duration = TimeSpan.FromMilliseconds(300);
        fadeAnim.Target = "Opacity";

        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        visual.StartAnimation("Offset.X", slideAnim);
        visual.StartAnimation("Opacity", fadeAnim);

        batch.Completed += (_, _) =>
        {
            Dismissed?.Invoke(this, EventArgs.Empty);
            Close();
        };

        batch.End();
    }
}
