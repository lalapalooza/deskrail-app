using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DeskRail.App.ViewModels;
using DeskRail.Core.Interop;
using DeskRail.Core.Models;
using DeskRail.Core.Services;
using Serilog;

namespace DeskRail.App.Views;

public partial class PanelWindow : Window
{
    private const int AnimationDuration = 300;
    private static readonly IReadOnlyDictionary<string, (string Dark, string Light)> ThemeColors =
        new Dictionary<string, (string, string)>
        {
            ["PanelBackgroundBrush"] = ("#F20B1018", "#F2F8F9FC"),
            ["TextPrimaryBrush"] = ("#F3F6FA", "#172033"),
            ["TextSecondaryBrush"] = ("#B3BECF", "#37465B"),
            ["TextMutedBrush"] = ("#8793A5", "#536174"),
            ["PlaceholderBrush"] = ("#8793A5", "#536174"),
            ["InputBackgroundBrush"] = ("#FF151C26", "#FFFFFFFF"),
            ["InputBorderBrush"] = ("#FF394556", "#FF9AA9BC"),
            ["ButtonBackgroundBrush"] = ("#FF1A222D", "#FFE8EDF4"),
            ["ButtonBorderBrush"] = ("#FF344154", "#FFA8B5C6"),
            ["HoverBackgroundBrush"] = ("#FF2B3542", "#FFD5E0EC"),
            ["SelectedBackgroundBrush"] = ("#FF182722", "#FFD9F4E9"),
            ["AccentBorderBrush"] = ("#A630EAB0", "#FF47A982"),
            ["AccentTextBrush"] = ("#FF00E5A0", "#FF087A59"),
            ["DividerBrush"] = ("#FF303B4A", "#FFD0D8E2"),
            ["ListItemBackgroundBrush"] = ("#FF151C26", "#FFE9EEF5"),
            ["ItemHoverBackgroundBrush"] = ("#4026C99A", "#FFD5F2E5")
        };

    private PanelViewModel? _panelVM;
    private IconGridViewModel? _iconGridVM;
    private ShellExecutor? _shellExecutor;
    private bool _isResizing;
    private NativeMethods.POINT _resizeStartScreenPoint;
    private double _resizeStartWidth;

    public PanelWindow()
    {
        InitializeComponent();
        ApplyTheme(GlassMode.Dark);
        WindowStartupLocation = WindowStartupLocation.Manual;
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        KeyDown += OnKeyDown;
        DataContextChanged += (s, e) =>
        {
            if (DataContext is MainViewModel mainVM)
            {
                _panelVM = mainVM.PanelVM;
                _iconGridVM = mainVM.IconGridVM;
                _panelVM.CloseRequested += AnimateClose;
                PanelTranslation.X = -_panelVM.PanelWidth;
            }
        };
    }

    public void SetShellExecutor(ShellExecutor executor) => _shellExecutor = executor;

    public void ApplyTheme(GlassMode mode)
    {
        var isDark = mode == GlassMode.Dark;
        foreach (var (key, colors) in ThemeColors)
        {
            var colorText = isDark ? colors.Dark : colors.Light;
            var color = (Color)ColorConverter.ConvertFromString(colorText);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            Resources[key] = brush;
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var darkModeResult = DwmInterop.EnableDarkMode(hwnd);
        if (darkModeResult < 0)
            Log.Warning("无法为面板启用DWM深色模式，HRESULT: 0x{HResult:X8}", darkModeResult);

        var backdropResult = DwmInterop.DisableSystemBackdrop(hwnd);
        if (backdropResult < 0)
            Log.Warning("无法关闭面板DWM系统背景，HRESULT: 0x{HResult:X8}", backdropResult);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Top = 0;
        if (_panelVM?.IsOpen != true)
            PanelTranslation.X = -ActualWidth;
    }

    public void ShowPanel()
    {
        PanelTranslation.BeginAnimation(TranslateTransform.XProperty, null);
        PanelTranslation.X = -GetPanelWidth();
        if (!IsVisible)
            Show();

        var hwnd = new WindowInteropHelper(this).Handle;
        if (!NativeMethods.SetWindowPos(
                hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE))
        {
            Log.Warning("无法将面板置于点击覆盖层上方，Win32错误码：{ErrorCode}",
                Marshal.GetLastWin32Error());
        }

        UpdateLayout();
        PanelTranslation.X = -GetPanelWidth();
        Activate();
        Log.Information("打开面板：数据项 {ItemCount}，筛选后 {VisibleItemCount}，窗口 {Width}x{Height}",
            _iconGridVM?.AllItems.Count ?? 0,
            _iconGridVM?.FilteredItems.Count ?? 0,
            ActualWidth,
            ActualHeight);

        AnimateOpen();
    }

    private void AnimateOpen()
    {
        if (_panelVM?.IsOpen != true || !IsVisible)
            return;

        PanelTranslation.BeginAnimation(TranslateTransform.XProperty, null);
        PanelTranslation.X = -GetPanelWidth();
        var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(AnimationDuration))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        PanelTranslation.BeginAnimation(TranslateTransform.XProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    private void AnimateClose()
    {
        var anim = new DoubleAnimation(0, -GetPanelWidth(), TimeSpan.FromMilliseconds(AnimationDuration))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.HoldEnd
        };
        anim.Completed += (s, e) =>
        {
            _panelVM?.CloseAnimationCompleted?.Invoke();
        };
        PanelTranslation.BeginAnimation(TranslateTransform.XProperty, anim, HandoffBehavior.SnapshotAndReplace);
    }

    private double GetPanelWidth()
        => ActualWidth > 0 ? ActualWidth : _panelVM?.PanelWidth ?? Width;

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            _panelVM?.CloseCommand.Execute(null);
    }

    private void OnToggleGlassMode(object sender, RoutedEventArgs e)
    {
        if (_panelVM != null)
        {
            _panelVM.GlassMode = _panelVM.GlassMode == GlassMode.Dark ? GlassMode.Light : GlassMode.Dark;
            ApplyTheme(_panelVM.GlassMode);
        }
    }

    private void OnClosePanel(object sender, RoutedEventArgs e)
        => _panelVM?.CloseCommand.Execute(null);

    private void OnSelectViewMode(object sender, RoutedEventArgs e)
    {
        if (_iconGridVM != null && sender is ToggleButton { Tag: ViewMode mode })
            _iconGridVM.ViewMode = mode;
    }

    private DesktopItem? GetClickedItem(object? sender)
    {
        if (sender is FrameworkElement fe && fe.DataContext is DesktopItem item)
            return item;
        return null;
    }

    private void OnIconClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            var item = GetClickedItem(sender);
            if (item != null && _shellExecutor != null)
            {
                _shellExecutor.Open(item.Path);
                _panelVM?.CloseCommand.Execute(null);
            }
        }
    }

    private void OnNativeContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        e.Handled = true;
        var item = GetClickedItem(sender);
        if (item is null || _shellExecutor is null)
            return;

        try
        {
            _shellExecutor.ShowContextMenu(item.Path, new WindowInteropHelper(this).Handle);
        }
        catch (Exception exception) when (exception is IOException
                                         or UnauthorizedAccessException
                                         or COMException
                                         or Win32Exception
                                         or ArgumentException)
        {
            Log.Error(exception, "无法显示桌面项目的 Shell 右键菜单：{Path}", item.Path);
            MessageBox.Show(this, $"无法显示该项目的右键菜单。\n{exception.Message}",
                "DeskRail", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnResizeStart(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border grip || e.ChangedButton != MouseButton.Left || _panelVM is null)
            return;

        if (!NativeMethods.GetCursorPos(out _resizeStartScreenPoint))
        {
            Log.Error("无法读取鼠标屏幕坐标，面板宽度调整未开始：{ErrorCode}",
                Marshal.GetLastWin32Error());
            return;
        }

        _isResizing = true;
        _resizeStartWidth = _panelVM.PanelWidth;
        if (!Mouse.Capture(this, CaptureMode.SubTree))
        {
            _isResizing = false;
            Log.Warning("无法捕获鼠标，面板宽度调整未开始");
            return;
        }

        Log.Information("开始拖动面板宽度：起始宽度 {Width}，捕获成功", _resizeStartWidth);
        e.Handled = true;
    }

    private void OnResizeMove(object sender, MouseEventArgs e)
    {
        if (!_isResizing || _panelVM is null)
            return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            FinishResize();
            return;
        }

        UpdatePanelWidth(GetResizeDelta());
        e.Handled = true;
    }

    private void OnResizeEnd(object sender, MouseButtonEventArgs e)
    {
        if (!_isResizing || e.ChangedButton != MouseButton.Left)
            return;

        UpdatePanelWidth(GetResizeDelta());
        FinishResize();
        e.Handled = true;
    }

    private double GetResizeDelta()
    {
        if (!NativeMethods.GetCursorPos(out var currentScreenPoint))
        {
            Log.Warning("无法读取拖动中的鼠标坐标：{ErrorCode}", Marshal.GetLastWin32Error());
            return 0;
        }

        var dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        return (currentScreenPoint.X - _resizeStartScreenPoint.X) / dpiScale;
    }

    private void FinishResize()
    {
        _isResizing = false;
        if (Mouse.Captured is not null)
            Mouse.Capture(null);
        Log.Information("面板宽度调整完成：ViewModel {Width}，窗口 {ActualWidth}",
            _panelVM?.PanelWidth ?? 0, ActualWidth);
    }

    private void UpdatePanelWidth(double delta)
    {
        if (_panelVM is not null)
        {
            var width = Math.Clamp(_resizeStartWidth + delta, 320, 800);
            _panelVM.PanelWidth = width;
            Width = width;
        }
    }
}
