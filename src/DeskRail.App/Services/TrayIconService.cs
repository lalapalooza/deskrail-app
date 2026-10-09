using System.Drawing;
using System.Windows;
using DeskRail.Core.Services;
using Hardcodet.Wpf.TaskbarNotification;
using DeskRail.App.ViewModels;
using Serilog;

namespace DeskRail.App.Services;

public class TrayIconService : IDisposable
{
    private readonly TaskbarIcon _trayIcon;
    private readonly Icon _trayIconImage;
    private readonly PanelViewModel _panelVM;
    private readonly ConfigService _configService;
    private readonly RegistryBridge _registryBridge;

    public TrayIconService(
        PanelViewModel panelVM,
        ConfigService configService,
        RegistryBridge registryBridge)
    {
        _panelVM = panelVM;
        _configService = configService;
        _registryBridge = registryBridge;
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法确定 DeskRail 可执行文件路径。");
        _trayIconImage = Icon.ExtractAssociatedIcon(executablePath)
            ?? throw new InvalidOperationException($"无法从应用程序提取图标：{executablePath}");
        _trayIcon = new TaskbarIcon
        {
            Icon = _trayIconImage,
            ToolTipText = "DeskRail",
            ContextMenu = BuildContextMenu()
        };
    }

    private System.Windows.Controls.ContextMenu BuildContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var openItem = new System.Windows.Controls.MenuItem { Header = "打开面板" };
        openItem.Click += (s, e) => _panelVM.OpenCommand.Execute(null);
        menu.Items.Add(openItem);

        var takeoverItem = new System.Windows.Controls.MenuItem
        {
            Header = "接管桌面图标",
            IsCheckable = true,
            IsChecked = _configService.Config.Takeover.Enabled
        };
        takeoverItem.Click += OnToggleTakeover;
        menu.Items.Add(takeoverItem);

        var autoStartItem = new System.Windows.Controls.MenuItem
        {
            Header = "开机自启动",
            IsCheckable = true,
            IsChecked = _configService.Config.Takeover.AutoStart
        };
        autoStartItem.Click += OnToggleAutoStart;
        menu.Items.Add(autoStartItem);
        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "退出" };
        exitItem.Click += (s, e) => Application.Current.Shutdown();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void OnToggleTakeover(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem item)
            return;

        var previousValue = _configService.Config.Takeover.Enabled;
        try
        {
            if (item.IsChecked)
            {
                _registryBridge.HideDesktopIcons();
                LockFile.Create();
            }
            else
            {
                _registryBridge.ShowDesktopIcons();
                LockFile.Delete();
            }

            _configService.Config.Takeover.Enabled = item.IsChecked;
            _configService.Save();
        }
        catch (Exception exception)
        {
            item.IsChecked = previousValue;
            _configService.Config.Takeover.Enabled = previousValue;
            Log.Error(exception, "切换桌面接管模式失败");
        }
    }

    private void OnToggleAutoStart(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem item)
            return;

        var previousValue = _configService.Config.Takeover.AutoStart;
        try
        {
            var executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("无法确定 DeskRail 可执行文件路径。");
            _registryBridge.SetAutoStart(item.IsChecked, executablePath);
            _configService.Config.Takeover.AutoStart = item.IsChecked;
            _configService.Save();
        }
        catch (Exception exception)
        {
            item.IsChecked = previousValue;
            _configService.Config.Takeover.AutoStart = previousValue;
            Log.Error(exception, "切换开机自启动失败");
        }
    }

    public void Dispose()
    {
        _trayIcon.Dispose();
        _trayIconImage.Dispose();
    }
}
