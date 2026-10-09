using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DeskRail.App.ViewModels;
using DeskRail.Core.Interop;
using Serilog;

namespace DeskRail.App.Views;

public partial class TriggerWindow : Window
{
    private readonly NativeMethods.WinEventDelegate _foregroundChangedCallback;
    private readonly DispatcherTimer _fullscreenCheckTimer;
    private nint _foregroundHook;

    public TriggerWindow()
    {
        InitializeComponent();
        _foregroundChangedCallback = OnForegroundChanged;
        _fullscreenCheckTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _fullscreenCheckTimer.Tick += (_, _) => UpdateFullscreenVisibility();
        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => UpdateScreenBounds();
        MouseDown += OnMouseDown;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
        UpdateScreenBounds();

        _foregroundHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            NativeMethods.EVENT_SYSTEM_FOREGROUND,
            nint.Zero,
            _foregroundChangedCallback,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT);
        if (_foregroundHook == nint.Zero)
            Log.Error("无法注册前台窗口变化监听，Win32错误码：{ErrorCode}", System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        _fullscreenCheckTimer.Start();
    }

    public void UpdateScreenBounds()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
        var screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
        if (screenWidth <= 0 || screenHeight <= 0)
        {
            Log.Error("无法取得主屏幕尺寸，触发条位置未更新：{Width}x{Height}", screenWidth, screenHeight);
            return;
        }

        const int hitAreaWidth = 14;
        var height = screenHeight / 2;
        var top = (screenHeight - height) / 2;
        if (!NativeMethods.SetWindowPos(
                hwnd, nint.Zero, 0, top, hitAreaWidth, height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER))
        {
            Log.Error("无法定位触发条，Win32错误码：{ErrorCode}", System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsFullscreenApplicationActive())
        {
            Hide();
            e.Handled = true;
            return;
        }

        if (DataContext is PanelViewModel vm)
            vm.OpenCommand.Execute(null);
    }

    public void ShowUnlessFullscreen()
    {
        if (IsFullscreenApplicationActive())
            Hide();
        else
            Show();
    }

    private void OnForegroundChanged(
        nint hook, uint eventType, nint hwnd, int idObject, int idChild,
        uint eventThread, uint eventTime)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(UpdateFullscreenVisibility);
            return;
        }

        UpdateFullscreenVisibility();
    }

    private void UpdateFullscreenVisibility()
    {
        if (IsFullscreenApplicationActive())
        {
            if (IsVisible)
                Hide();
        }
        else if (DataContext is PanelViewModel { IsOpen: false } && !IsVisible)
        {
            Show();
        }
    }

    private static bool IsFullscreenApplicationActive()
    {
        var foregroundWindow = NativeMethods.GetForegroundWindow();
        if (foregroundWindow == nint.Zero)
            return false;

        NativeMethods.GetWindowThreadProcessId(foregroundWindow, out var processId);
        if (processId == (uint)Environment.ProcessId)
            return false;

        var screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
        var screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);
        if (screenWidth <= 0 || screenHeight <= 0
            || !NativeMethods.GetWindowRect(foregroundWindow, out var bounds))
            return false;

        return bounds.Left <= 1
               && bounds.Top <= 1
               && bounds.Right >= screenWidth - 1
               && bounds.Bottom >= screenHeight - 1;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _fullscreenCheckTimer.Stop();
        if (_foregroundHook != nint.Zero)
        {
            if (!NativeMethods.UnhookWinEvent(_foregroundHook))
                Log.Warning("无法注销前台窗口变化监听，Win32错误码：{ErrorCode}", System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            _foregroundHook = nint.Zero;
        }
    }

    private void OnTriggerMouseEnter(object sender, MouseEventArgs e)
        => AnimateGlow(isHovered: true);

    private void OnTriggerMouseLeave(object sender, MouseEventArgs e)
        => AnimateGlow(isHovered: false);

    private void AnimateGlow(bool isHovered)
    {
        var width = new DoubleAnimation
        {
            To = isHovered ? 7 : 2,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        TriggerLine.BeginAnimation(FrameworkElement.WidthProperty, width, HandoffBehavior.SnapshotAndReplace);

        var lineOpacity = new DoubleAnimation
        {
            To = isHovered ? 1 : 0.72,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        TriggerLine.BeginAnimation(FrameworkElement.OpacityProperty, lineOpacity, HandoffBehavior.SnapshotAndReplace);

        var glowOpacity = new DoubleAnimation
        {
            To = isHovered ? 0.9 : 0.55,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        TriggerGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
            glowOpacity, HandoffBehavior.SnapshotAndReplace);
    }
}
