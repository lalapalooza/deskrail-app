# DeskRail 详细设计方案 & Agent提示词文档

> 来源: https://chat.z.ai/space/v1mh68b2njr0-art
> 获取时间: 2026-10-08

## 01 项目概述

DeskRail 是一款 Windows 桌面增强工具。它接管系统桌面图标的显示与管理职责，以最小视觉占用的触发竖线常驻屏幕左侧边缘，点击后从左侧展开磨砂玻璃面板，以多行流式布局重新呈现所有桌面内容（图标、文件、目录、快捷方式），并提供搜索、排序、间距调整、视图切换等增强功能。

**核心价值主张:**
- 零干扰常驻：收起态仅占用1px宽度 + 呼吸脉动，不遮挡任何桌面内容。
- 一键触达：单击即展开全功能面板，比传统桌面更高效的内容组织方式。
- 原生体验：WPF原生渲染 + 直接P/Invoke Win32 API，无Chromium开销，内存占用低，启动快。

## 02 技术架构

| 层级 | 技术 | 版本 | 用途 |
|---|---|---|---|
| 运行时 | .NET | 8.0 LTS | 应用运行时，自包含发布无需用户安装 |
| UI框架 | WPF | 内置 | 窗口管理、XAML布局、数据绑定、样式模板 |
| 架构模式 | MVVM | - | View(XAML) + ViewModel + Model 分离 |
| MVVM工具包 | CommunityToolkit.Mvvm | 8.x | ObservableObject, RelayCommand, Source Generator |
| Win32互操作 | P/Invoke + CsWin32 | - | 直接调用Win32 API，零开销桥接 |
| Shell COM | Shell32 / OLE32 COM | 系统内置 | IShellFolder, IContextMenu, SHGetFileInfo |
| 图标提取 | System.Drawing | 内置 | Icon.ExtractAssociatedIcon + SHGetFileInfo |
| 注册表 | Microsoft.Win32.Registry | 内置 | 桌面图标显隐控制 |
| 文件监听 | FileSystemWatcher | 内置 | 桌面目录变更实时通知 |
| 依赖注入 | Microsoft.Extensions.DI | 8.x | 服务注册与生命周期管理 |
| 配置 | Options pattern + JSON | 内置 | 强类型配置 + 持久化 |
| 日志 | Serilog | 3.x | 结构化日志，文件+事件日志输出 |
| 打包分发 | Inno Setup | 6.x | NSIS风格安装包，自动更新友好 |

## 03 项目目录结构

```
DeskRail/
├── src/
│   ├── DeskRail.App/                   # 主WPF应用项目
│   │   ├── App.xaml / App.xaml.cs   # 应用入口、DI容器、生命周期
│   │   ├── Views/                  # XAML视图
│   │   │   ├── TriggerWindow.xaml      # 触发竖线窗口
│   │   │   ├── PanelWindow.xaml        # 面板主窗口
│   │   │   ├── OverlayWindow.xaml      # 全屏透明遮罩
│   │   │   └── ContextMenuPopup.xaml   # 自定义右键菜单
│   │   ├── Controls/               # 自定义WPF控件
│   │   │   ├── IconItem.xaml
│   │   │   ├── GlassPanel.cs
│   │   │   ├── ResizeGrip.cs
│   │   │   └── SearchBox.xaml
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs
│   │   │   ├── PanelViewModel.cs
│   │   │   ├── IconGridViewModel.cs
│   │   │   └── ConfigViewModel.cs
│   │   ├── Styles/
│   │   │   ├── Colors.xaml
│   │   │   ├── Typography.xaml
│   │   │   ├── IconItemStyles.xaml
│   │   │   └── GlassStyles.xaml
│   │   ├── Converters/
│   │   └── Assets/
│   ├── DeskRail.Core/                 # 核心业务(无UI依赖)
│   │   ├── Models/
│   │   │   ├── DesktopItem.cs
│   │   │   ├── FileTypeInfo.cs
│   │   │   └── AppConfig.cs
│   │   ├── Services/
│   │   │   ├── DesktopScanner.cs
│   │   │   ├── IconExtractor.cs
│   │   │   ├── ShellExecutor.cs
│   │   │   ├── RegistryBridge.cs
│   │   │   ├── DesktopWatcher.cs
│   │   │   └── ConfigService.cs
│   │   └── Interop/
│   │       ├── NativeMethods.cs
│   │       ├── ShellCOM.cs
│   │       ├── DwmInterop.cs
│   │       └── Shell32Interop.cs
│   └── DeskRail.Setup/
│       └── setup.iss
├── DeskRail.sln
├── Directory.Build.props
└── global.json
```

## Agent提示词文档

### P0 项目初始化与脚手架

任务：
1. 解决方案结构：DeskRail.sln + src/DeskRail.App + src/DeskRail.Core + src/DeskRail.Setup
2. 项目配置：global.json, Directory.Build.props, NuGet包
3. 核心模型：DesktopItem.cs, FileType枚举, AppConfig.cs
4. DI与启动骨架 (App.xaml.cs)
5. ViewModel骨架

验收标准：
- dotnet build 无错误
- dotnet run 可启动WPF应用
- DI容器正确构建
- 单实例锁生效

### P1 窗口管理与触发线

任务：
1. TriggerWindow (1px竖线 + 呼吸动画 + hover扩展)
2. PanelWindow (Acrylic玻璃背景)
3. DwmInterop (DWMWA_SYSTEMBACKDROP_TYPE=3)
4. 面板展开/收起动画 (450ms easeOutCubic)
5. OverlayWindow (点击外部关闭)
6. 系统托盘
7. Win32窗口增强

验收标准：
- 启动后屏幕左侧可见1px绿色竖线，带呼吸脉动
- 点击竖线→面板从左侧450ms滑入
- 点击外部/ESC→面板滑出

### P2 桌面扫描与图标提取

任务：
1. DesktopScanner (扫描用户+公共桌面)
2. IconExtractor (SHGetFileInfo → BitmapSource)
3. Shell32Interop
4. DesktopWatcher (FileSystemWatcher, 200ms防抖)
5. .lnk快捷方式解析

### P3 面板UI完整实现

任务：
1. PanelHeader, SearchBox, ControlsBar, SortToolbar
2. IconGrid + IconItem (按类型着色)
3. ContextMenu
4. StatusBar, ResizeGrip
5. 样式资源

### P4 注册表接管与系统集成

任务：
1. RegistryBridge (HideDesktopIcons/ShowDesktopIcons)
2. LockFile机制
3. ShellExecutor完善
4. 开机自启动
5. ConfigService

### P5 打包、优化与发布

任务：
1. dotnet publish自包含单文件
2. Inno Setup打包
3. 性能优化 (虚拟化)
4. Serilog日志 + 全局异常处理
5. 最终测试
