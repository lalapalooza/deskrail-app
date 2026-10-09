using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using DeskRail.Core.Interop;
using DeskRail.Core.Models;

namespace DeskRail.Core.Services;

public class DesktopScanner
{
    private const string ThisPcClsid = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    private const string RecycleBinClsid = "::{645FF040-5081-101B-9F08-00AA002F954E}";
    private readonly IconExtractor _iconExtractor;
    private readonly ConfigService _configService;

    public DesktopScanner(IconExtractor iconExtractor, ConfigService configService)
    {
        _iconExtractor = iconExtractor;
        _configService = configService;
    }

    public static (string User, string Public) GetDesktopPaths()
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        return (user, publicDesktop);
    }

    public async Task<List<DesktopItem>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var (userDirectory, publicDirectory) = GetDesktopPaths();
        return await Task.Run(() =>
        {
            var systemItems = new List<DesktopItem>();
            AddSystemItem(systemItems, "此电脑", ThisPcClsid);
            AddSystemItem(systemItems, "回收站", RecycleBinClsid);
            var desktopItems = new List<DesktopItem>();

            foreach (var directory in new[] { userDirectory, publicDirectory })
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                    continue;

                try
                {
                    foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var attributes = File.GetAttributes(entry);
                            if (ShouldSkipEntry(entry, attributes))
                                continue;

                            desktopItems.Add(ResolveItem(entry));
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // Desktop entries may disappear or become inaccessible while scanning.
                        }
                        catch (IOException)
                        {
                            // Ignore an entry that was removed or changed during enumeration.
                        }
                        catch (System.Security.SecurityException)
                        {
                            // Ignore an entry that cannot be accessed by the current user.
                        }
                        catch (COMException)
                        {
                            // A broken shell shortcut should not prevent other items from loading.
                        }
                        catch (ArgumentException)
                        {
                            // Ignore malformed paths returned during enumeration.
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Continue scanning the other desktop folder if this one is inaccessible.
                }
                catch (IOException)
                {
                    // Continue scanning the other desktop folder if it changes during enumeration.
                }
            }

            return systemItems.Concat(Deduplicate(desktopItems)).ToList();
        }, cancellationToken).ConfigureAwait(false);
    }

    private DesktopItem ResolveItem(string path)
    {
        var attributes = File.GetAttributes(path);
        var isDirectory = attributes.HasFlag(FileAttributes.Directory) || Directory.Exists(path);
        var extension = Path.GetExtension(path);
        var name = extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(path)
            : Path.GetFileName(path);
        string? shortcutTarget = null;

        var type = isDirectory
            ? FileType.Folder
            : extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                ? ResolveShortcutType(shortcutTarget = ResolveShortcutTarget(path))
                : GetFileType(path, extension);

        var modified = isDirectory
            ? Directory.GetLastWriteTime(path)
            : File.GetLastWriteTime(path);
        var size = isDirectory ? 0 : new FileInfo(path).Length;

        return new DesktopItem
        {
            Name = name,
            Path = path,
            Type = type,
            IconSource = ExtractIconOrNull(path, type),
            Size = size,
            SizeDisplay = isDirectory ? string.Empty : FormatSize(size),
            Modified = modified,
            Extension = extension,
            IsPinned = false,
            ShortcutTarget = shortcutTarget
        };
    }

    private bool ShouldSkipEntry(string path, FileAttributes attributes)
    {
        if (Path.GetFileName(path).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
            return true;

        return !_configService.Config.General.ShowHiddenFiles
               && (attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;
    }

    private static FileType GetFileType(string path, string extension)
    {
        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".msi", StringComparison.OrdinalIgnoreCase))
            return FileType.App;

        if (Directory.Exists(path))
            return FileType.Folder;

        return FileTypeInfo.MapExtension(extension);
    }

    private static FileType ResolveShortcutType(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return FileType.Shortcut;

        return GetFileType(target, Path.GetExtension(target));
    }

    private static string? ResolveShortcutTarget(string shortcutPath)
    {
        Shell32Interop.ShellLink? shellLink = null;
        try
        {
            shellLink = new Shell32Interop.ShellLink();
            var shellLinkInterface = (Shell32Interop.IShellLinkW)shellLink;
            var persistFile = (IPersistFile)shellLink;
            persistFile.Load(shortcutPath, 0);

            var target = new StringBuilder(32768);
            var findData = default(Shell32Interop.WIN32_FIND_DATAW);
            shellLinkInterface.GetPath(target, target.Capacity, ref findData, 0);
            var result = target.ToString();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (shellLink is not null && Marshal.IsComObject(shellLink))
                Marshal.ReleaseComObject(shellLink);
        }
    }

    private void AddSystemItem(ICollection<DesktopItem> items, string name, string clsid)
    {
        try
        {
            items.Add(new DesktopItem
            {
                Name = name,
                Path = clsid,
                Type = FileType.System,
                IconSource = ExtractIconOrNull(clsid, FileType.System),
                Size = 0,
                SizeDisplay = string.Empty,
                Modified = DateTime.MinValue,
                Extension = string.Empty,
                IsPinned = false
            });
        }
        catch (Exception exception) when (exception is IOException
                                         or UnauthorizedAccessException
                                         or System.Security.SecurityException
                                         or COMException
                                         or ArgumentException
                                         or ExternalException)
        {
            System.Diagnostics.Trace.TraceError("无法加载系统桌面项目 {0}：{1}", name, exception);
        }
    }

    private byte[]? ExtractIconOrNull(string path, FileType type)
    {
        try
        {
            return _iconExtractor.Extract(path, type);
        }
        catch (Exception exception) when (exception is IOException
                                         or UnauthorizedAccessException
                                         or System.Security.SecurityException
                                         or COMException
                                         or ArgumentException
                                         or ExternalException)
        {
            System.Diagnostics.Trace.TraceError("无法提取项目图标 {0}：{1}", path, exception);
            return null;
        }
    }

    private static List<DesktopItem> Deduplicate(IEnumerable<DesktopItem> items)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<DesktopItem>();
        foreach (var item in items)
        {
            if (seen.Add(item.Name))
                result.Add(item);
        }

        return result;
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }
}

[ComImport]
[Guid("0000010b-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IPersistFile
{
    void GetClassID(out Guid pClassID);
    void IsDirty();
    void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
    void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
    void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
    void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
}
