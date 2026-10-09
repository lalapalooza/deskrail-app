using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Threading;
using DeskRail.App.Services;
using DeskRail.App.ViewModels;
using DeskRail.App.Views;
using DeskRail.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DeskRail.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private TriggerWindow? _triggerWindow;
    private PanelWindow? _panelWindow;
    private OverlayWindow? _overlayWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 单实例锁
        _singleInstanceMutex = new Mutex(true, @"Local\DeskRail_SingleInstance", out var createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }
        _ownsSingleInstanceMutex = true;

        // 配置 Serilog
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DeskRail", "log.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        // 全局异常处理
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // 构建DI容器
        _services = new ServiceCollection()
            .AddSingleton<ConfigService>()
            .AddSingleton<IconExtractor>()
            .AddSingleton<DesktopScanner>()
            .AddSingleton<ShellExecutor>()
            .AddSingleton<RegistryBridge>()
            .AddSingleton<DesktopWatcher>()
            .AddSingleton<MainViewModel>()
            .AddSingleton<PanelViewModel>()
            .AddSingleton<IconGridViewModel>()
            .AddSingleton<TrayIconService>()
            .AddLogging(builder => builder.AddSerilog(dispose: true))
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        Log.Information("DeskRail 启动");

        // 加载配置
        var configService = _services.GetRequiredService<ConfigService>();
        configService.Load();

        // 锁文件检测 + 崩溃恢复
        var registry = _services.GetRequiredService<RegistryBridge>();
        if (LockFile.Exists())
        {
            Log.Warning("检测到锁文件，执行崩溃恢复");
            registry.ShowDesktopIcons();
            LockFile.Delete();
        }

        // 启动文件监听
        _services.GetRequiredService<DesktopWatcher>().Start();
        _services.GetRequiredService<TrayIconService>();

        var mainVM = _services.GetRequiredService<MainViewModel>();
        await mainVM.IconGridVM.RefreshCommand.ExecuteAsync(null);
        Log.Information("启动扫描完成：桌面项目 {ItemCount} 个，面板显示 {VisibleItemCount} 个",
            mainVM.IconGridVM.AllItems.Count, mainVM.IconGridVM.FilteredItems.Count);
        var panelVM = mainVM.PanelVM;
        panelVM.PanelWidth = configService.Config.Panel.Width;
        panelVM.GlassMode = ReadWindowsTheme();

        // 创建窗口
        _triggerWindow = new TriggerWindow { DataContext = panelVM };
        _panelWindow = new PanelWindow { DataContext = mainVM };
        _panelWindow.ApplyTheme(panelVM.GlassMode);
        _panelWindow.SetShellExecutor(_services.GetRequiredService<ShellExecutor>());
        _overlayWindow = new OverlayWindow { DataContext = panelVM };

        // 面板开合协调
        panelVM.OpenRequested += () =>
        {
            _triggerWindow.Hide();
            _overlayWindow.UpdateScreenBounds();
            _overlayWindow.Show();
            _panelWindow.ShowPanel();
        };

        panelVM.CloseAnimationCompleted += () =>
        {
            _overlayWindow.Hide();
            _panelWindow.Hide();
            _triggerWindow.ShowUnlessFullscreen();
        };

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _triggerWindow.ShowUnlessFullscreen();
        using (var process = System.Diagnostics.Process.GetCurrentProcess())
        {
            process.Refresh();
            Log.Information(
                "启动资源快照：工作集 {WorkingSetMb:F1} MiB，私有内存 {PrivateMemoryMb:F1} MiB，托管堆 {ManagedHeapMb:F1} MiB",
                process.WorkingSet64 / 1024d / 1024d,
                process.PrivateMemorySize64 / 1024d / 1024d,
                GC.GetTotalMemory(false) / 1024d / 1024d);
        }

        // 接管模式
        if (configService.Config.Takeover.Enabled)
        {
            registry.HideDesktopIcons();
            LockFile.Create();
        }

    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "未处理的UI线程异常");
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Log.Fatal(ex, "未处理的域异常");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "未观察到的Task异常");
        e.SetObserved();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_services is null)
                return;

            _services.GetRequiredService<MainViewModel>().PanelVM.RefreshScreenMetrics();
            _triggerWindow?.UpdateScreenBounds();
            _overlayWindow?.UpdateScreenBounds();
        });
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color))
            return;

        Dispatcher.BeginInvoke(() =>
        {
            if (_services is null)
                return;

            var theme = ReadWindowsTheme();
            _services.GetRequiredService<MainViewModel>().PanelVM.GlassMode = theme;
            _panelWindow?.ApplyTheme(theme);
            Log.Information("已根据 Windows 应用主题更新面板颜色：{Theme}",
                theme == DeskRail.Core.Models.GlassMode.Dark ? "Dark" : "Light");
        });
    }

    private static DeskRail.Core.Models.GlassMode ReadWindowsTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme")
                ?? key?.GetValue("SystemUsesLightTheme");
            return value is int lightTheme && lightTheme == 0
                ? DeskRail.Core.Models.GlassMode.Dark
                : DeskRail.Core.Models.GlassMode.Light;
        }
        catch (Exception exception) when (exception is IOException
                                         or UnauthorizedAccessException
                                         or System.Security.SecurityException)
        {
            Log.Warning(exception, "读取 Windows 主题失败，回退到亮色主题");
            return DeskRail.Core.Models.GlassMode.Light;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("DeskRail 退出");
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        if (_services is not null)
        {
            var configService = _services.GetRequiredService<ConfigService>();
            var panelVM = _services.GetRequiredService<MainViewModel>().PanelVM;
            configService.Config.Panel.Width = panelVM.PanelWidth;
            configService.Config.Panel.GlassMode = panelVM.GlassMode;
            if (LockFile.Exists() || configService.Config.Takeover.Enabled)
                _services.GetRequiredService<RegistryBridge>().ShowDesktopIcons();
            LockFile.Delete();
            configService.Save();
        }

        _services?.Dispose();
        if (_ownsSingleInstanceMutex)
            _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
