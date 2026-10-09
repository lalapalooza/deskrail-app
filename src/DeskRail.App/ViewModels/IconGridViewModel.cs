using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskRail.Core.Models;
using DeskRail.Core.Services;
using Serilog;
using System.Windows.Threading;

namespace DeskRail.App.ViewModels;

public partial class IconGridViewModel : ObservableObject
{
    private readonly DesktopScanner _scanner;
    private readonly DesktopWatcher _watcher;
    private readonly Dispatcher _dispatcher;

    [ObservableProperty]
    private List<DesktopItem> _allItems = [];

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private bool _refreshFailed;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredItems));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    partial void OnAllItemsChanged(List<DesktopItem> value)
    {
        OnPropertyChanged(nameof(FilteredItems));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    partial void OnIsRefreshingChanged(bool value) => OnPropertyChanged(nameof(EmptyStateMessage));
    partial void OnRefreshFailedChanged(bool value) => OnPropertyChanged(nameof(EmptyStateMessage));

    [ObservableProperty]
    private SortKey _sortKey = SortKey.Name;

    partial void OnSortKeyChanged(SortKey value) => OnPropertyChanged(nameof(FilteredItems));

    [ObservableProperty]
    private bool _sortAscending = true;

    partial void OnSortAscendingChanged(bool value) => OnPropertyChanged(nameof(FilteredItems));

    [ObservableProperty]
    private ViewMode _viewMode = ViewMode.Grid;

    [ObservableProperty]
    private double _colGap = 16;

    [ObservableProperty]
    private double _rowGap = 12;

    public IconGridViewModel(DesktopScanner scanner, DesktopWatcher watcher)
    {
        _scanner = scanner;
        _watcher = watcher;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _watcher.DesktopChanged += OnDesktopChanged;
        _watcher.ScanFailed += exception => Log.Error(exception, "桌面变更后扫描失败");
    }

    public string EmptyStateMessage
    {
        get
        {
            if (IsRefreshing)
                return "正在加载桌面项目...";
            if (RefreshFailed)
                return "桌面项目加载失败，请点击刷新重试。";
            return string.IsNullOrWhiteSpace(SearchQuery)
                ? "桌面上暂无项目"
                : "没有匹配的项目";
        }
    }

    private void OnDesktopChanged(object? sender, List<DesktopItem> items)
    {
        _dispatcher.BeginInvoke(() =>
        {
            AllItems = items;
            RefreshFailed = false;
            Log.Information("检测到桌面内容变更：更新为 {ItemCount} 个项目", items.Count);
        });
    }

    public List<DesktopItem> FilteredItems
    {
        get
        {
            var matchingItems = AllItems
                .Where(i => string.IsNullOrEmpty(SearchQuery)
                         || i.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var systemItems = matchingItems.Where(item => item.Type == FileType.System);
            var desktopItems = matchingItems
                .Where(item => item.Type != FileType.System)
                .OrderBy(item => item.IsPinned ? 0 : 1);

            var sortedDesktopItems = SortKey switch
            {
                SortKey.Name => SortAscending
                    ? desktopItems.ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                    : desktopItems.ThenByDescending(item => item.Name, StringComparer.CurrentCultureIgnoreCase),
                SortKey.Type => SortAscending
                    ? desktopItems.ThenBy(item => item.Type)
                    : desktopItems.ThenByDescending(item => item.Type),
                SortKey.Size => SortAscending
                    ? desktopItems.ThenBy(item => item.Size)
                    : desktopItems.ThenByDescending(item => item.Size),
                SortKey.Date => SortAscending
                    ? desktopItems.ThenBy(item => item.Modified)
                    : desktopItems.ThenByDescending(item => item.Modified),
                _ => desktopItems.ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            };

            return systemItems.Concat(sortedDesktopItems).ToList();
        }
    }

    [RelayCommand]
    private async Task Refresh()
    {
        RefreshFailed = false;
        IsRefreshing = true;
        try
        {
            AllItems = await _scanner.ScanAsync();
            Log.Information("桌面扫描成功：发现 {ItemCount} 个项目", AllItems.Count);
        }
        catch (Exception exception) when (exception is IOException
                                         or UnauthorizedAccessException
                                         or System.Security.SecurityException
                                         or System.Runtime.InteropServices.COMException
                                         or ArgumentException
                                         or InvalidOperationException)
        {
            RefreshFailed = true;
            Log.Error(exception, "扫描桌面项目失败");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private void ToggleSort(SortKey key)
    {
        if (SortKey == key) SortAscending = !SortAscending;
        else { SortKey = key; SortAscending = true; }
        OnPropertyChanged(nameof(FilteredItems));
    }
}
