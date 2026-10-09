# DeskRail

DeskRail 是一款 Windows 桌面增强工具：通过屏幕左侧的触发线打开桌面面板，集中浏览和管理桌面项目。

## 功能

- 扫描用户桌面和公共桌面，并实时响应文件变化
- 在侧边面板中搜索、排序和切换桌面项目视图
- 支持系统托盘、快捷方式和系统桌面项目
- 使用 WPF 构建原生 Windows 界面

## 环境要求

- Windows 10 或更高版本
- .NET 8 SDK（开发和构建）
- .NET 8 Desktop Runtime（运行框架依赖型构建）

## 构建和运行

在仓库根目录执行：

```powershell
dotnet build .\DeskRail.sln
dotnet run --project .\src\DeskRail.App\DeskRail.App.csproj
```

启动后，点击屏幕左侧的触发线即可打开面板。应用配置和日志保存在当前用户的 `%AppData%\DeskRail` 目录。

## 发布

生成适用于 Windows x64 的单文件发布输出：

```powershell
dotnet publish .\src\DeskRail.App\DeskRail.App.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained false `
  -p:PublishSingleFile=true `
  --output .\publish
```

当前发布配置为框架依赖型，需要目标计算机安装 .NET 8 Desktop Runtime。安装包脚本位于 `src\DeskRail.Setup\setup.iss`，使用 Inno Setup 编译前请先生成 `publish` 目录。

## 项目结构

- `src\DeskRail.App`：WPF 应用、视图和 ViewModel
- `src\DeskRail.Core`：桌面扫描、文件监听、图标和 Shell 相关服务
- `src\DeskRail.Setup`：Inno Setup 安装脚本
