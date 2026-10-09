using System.IO;
using DeskRail.Core.Models;

namespace DeskRail.Core.Services;

public sealed class DesktopWatcher : IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(200);

    private readonly DesktopScanner _scanner;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly object _sync = new();
    private readonly Timer _debounceTimer;
    private bool _scanInProgress;
    private bool _scanAgain;
    private bool _disposed;
    private bool _started;

    public event EventHandler<List<DesktopItem>>? DesktopChanged;
    public event Action<Exception>? ScanFailed;

    public DesktopWatcher(DesktopScanner scanner)
    {
        _scanner = scanner;
        _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Start()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
                return;

            _started = true;
        }

        var (userDirectory, publicDirectory) = DesktopScanner.GetDesktopPaths();
        foreach (var directory in new[] { userDirectory, publicDirectory }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                continue;

            var watcher = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                IncludeSubdirectories = false,
                EnableRaisingEvents = false
            };

            watcher.Created += OnChanged;
            watcher.Deleted += OnChanged;
            watcher.Renamed += OnChanged;
            watcher.Changed += OnChanged;
            watcher.Error += OnWatcherError;

            lock (_sync)
            {
                if (_disposed)
                {
                    watcher.Dispose();
                    return;
                }

                _watchers.Add(watcher);
                watcher.EnableRaisingEvents = true;
            }
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => ScheduleScan();

    private void OnWatcherError(object sender, ErrorEventArgs e) => ScheduleScan();

    private void ScheduleScan()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            if (_scanInProgress)
            {
                _scanAgain = true;
                return;
            }

            _debounceTimer.Change(DebounceInterval, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            if (_scanInProgress)
            {
                _scanAgain = true;
                return;
            }

            _scanInProgress = true;
        }

        _ = ScanAsync();
    }

    private async Task ScanAsync()
    {
        try
        {
            while (true)
            {
                var items = await _scanner.ScanAsync().ConfigureAwait(false);
                DesktopChanged?.Invoke(this, items);

                lock (_sync)
                {
                    if (_disposed || !_scanAgain)
                    {
                        _scanInProgress = false;
                        return;
                    }

                    _scanAgain = false;
                }
            }
        }
        catch (Exception exception)
        {
            lock (_sync)
            {
                _scanInProgress = false;
                _scanAgain = false;
            }

            ScanFailed?.Invoke(exception);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;

            _disposed = true;
            _debounceTimer.Dispose();
            foreach (var watcher in _watchers)
                watcher.Dispose();
            _watchers.Clear();
        }
    }
}
