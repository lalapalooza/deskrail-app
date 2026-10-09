using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Drawing.Drawing2D;
using DeskRail.Core.Interop;
using DeskRail.Core.Models;

namespace DeskRail.Core.Services;

public class IconExtractor
{
    private const int MaxCachedIcons = 128;
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    public byte[] Extract(string path, FileType type)
    {
        var lastWriteTicks = GetLastWriteTicks(path);
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(path, out var cached)
                && cached.LastWriteTicks == lastWriteTicks)
            {
                return cached.Bytes;
            }
        }

        var bytes = ExtractUncached(path, type);

        lock (_cacheLock)
        {
            if (!_cache.ContainsKey(path) && _cache.Count >= MaxCachedIcons)
                _cache.Remove(_cache.Keys.First());

            _cache[path] = new CacheEntry(lastWriteTicks, bytes);
        }

        return bytes;
    }

    private static byte[] ExtractUncached(string path, FileType type)
    {
        if (ShouldUseIllustratedIcon(path, type))
            return CreateTypeIcon(type, path);

        var shfi = default(Shell32Interop.SHFILEINFO);
        var flags = Shell32Interop.SHGFI_ICON | Shell32Interop.SHGFI_LARGEICON;

        if (Shell32Interop.SHGetFileInfo(path, 0, ref shfi, Marshal.SizeOf<Shell32Interop.SHFILEINFO>(), flags) == nint.Zero)
            return CreateTypeIcon(type, path);

        if (shfi.hIcon == nint.Zero)
            return CreateTypeIcon(type, path);

        try
        {
            using var icon = (Icon)Icon.FromHandle(shfi.hIcon).Clone();
            using var bmp = icon.ToBitmap();
            using var stream = new MemoryStream();
            bmp.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
        finally
        {
            if (shfi.hIcon != nint.Zero)
                Shell32Interop.DestroyIcon(shfi.hIcon);
        }
    }

    private static bool ShouldUseIllustratedIcon(string path, FileType type)
        => type is not (FileType.App or FileType.Shortcut or FileType.System)
           && !Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase);

    private static long GetLastWriteTicks(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path).Ticks;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private sealed record CacheEntry(long LastWriteTicks, byte[] Bytes);

    private static byte[] CreateTypeIcon(FileType type, string path)
    {
        var color = type switch
        {
            FileType.Folder => "#FFB347",
            FileType.App => "#00E5A0",
            FileType.Document => "#60A5FA",
            FileType.Image => "#E879F9",
            FileType.Music => "#F472B6",
            FileType.Video => "#FB923C",
            FileType.Archive => "#A78BFA",
            FileType.System => "#4E5A6E",
            FileType.Code => "#47B3FF",
            _ => "#888888"
        };

        using var bitmap = new Bitmap(48, 48);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(Color.Transparent);
            var accent = ColorTranslator.FromHtml(color);

            if (type == FileType.Folder)
            {
                DrawFolder(graphics, accent);
            }
            else if (type == FileType.System)
            {
                DrawSystemIcon(graphics, path, accent);
            }
            else
            {
                DrawDocument(graphics, accent);
                DrawTypeMark(graphics, type, path, accent);
            }
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static void DrawFolder(Graphics graphics, Color accent)
    {
        using var path = new GraphicsPath();
        path.AddPolygon(Points(5, 13, 5, 10, 8, 8, 19, 8, 24, 13, 41, 13, 44, 16, 42, 39, 39, 42, 8, 42, 5, 39));
        using var brush = new SolidBrush(accent);
        graphics.FillPath(brush, path);

        using var highlight = new SolidBrush(Color.FromArgb(210, Color.White));
        graphics.FillPolygon(highlight, Points(7, 16, 42, 16, 40, 19, 6, 19));
    }

    private static void DrawDocument(Graphics graphics, Color accent)
    {
        using var path = new GraphicsPath();
        path.AddPolygon(Points(12, 4, 30, 4, 39, 13, 39, 42, 9, 42, 9, 7));
        using var brush = new SolidBrush(accent);
        graphics.FillPath(brush, path);
        graphics.FillPolygon(Brushes.White, Points(30, 5, 30, 14, 38, 14));
    }

    private static void DrawTypeMark(Graphics graphics, FileType type, string path, Color accent)
    {
        var extension = Path.GetExtension(path);
        var mark = type switch
        {
            FileType.Document => extension.ToLowerInvariant() switch
            {
                ".doc" or ".docx" => "W",
                ".xls" or ".xlsx" => "X",
                ".ppt" or ".pptx" => "P",
                ".pdf" => "PDF",
                _ => "≡"
            },
            FileType.Image or FileType.Music or FileType.Video or FileType.Archive => string.Empty,
            FileType.Code => "</>",
            _ => "•"
        };

        var fontSize = mark.Length > 2 ? 8 : 14;
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.FromArgb(245, 8, 12, 18));
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.DrawString(mark, font, brush, new RectangleF(9, 19, 30, 19), format);

        if (type == FileType.Image)
        {
            using var detailBrush = new SolidBrush(Color.FromArgb(230, 8, 12, 18));
            graphics.FillEllipse(detailBrush, 17, 22, 4, 4);
            graphics.FillPolygon(detailBrush, Points(13, 36, 21, 28, 26, 34, 30, 30, 36, 37));
        }
        else if (type == FileType.Music)
        {
            using var detailBrush = new SolidBrush(Color.FromArgb(230, 8, 12, 18));
            graphics.FillEllipse(detailBrush, 15, 33, 6, 5);
            graphics.FillRectangle(detailBrush, 20, 23, 2, 12);
            graphics.FillEllipse(detailBrush, 25, 30, 6, 5);
            graphics.FillRectangle(detailBrush, 30, 19, 2, 12);
            graphics.FillPolygon(detailBrush, Points(21, 22, 32, 18, 32, 21, 21, 25));
        }
        else if (type == FileType.Video)
        {
            using var detailBrush = new SolidBrush(Color.FromArgb(230, 8, 12, 18));
            graphics.FillPolygon(detailBrush, Points(19, 24, 31, 30, 19, 36));
        }
        else if (type == FileType.Archive)
        {
            using var detailBrush = new SolidBrush(Color.FromArgb(230, 8, 12, 18));
            graphics.FillRectangle(detailBrush, 23, 19, 3, 16);
            for (var y = 21; y < 34; y += 4)
                graphics.FillRectangle(Brushes.White, 23, y, 3, 2);
        }
    }

    private static void DrawSystemIcon(Graphics graphics, string path, Color accent)
    {
        using var brush = new SolidBrush(accent);
        if (path.Contains("645FF040", StringComparison.OrdinalIgnoreCase))
        {
            graphics.FillPolygon(brush, Points(12, 12, 36, 12, 34, 40, 14, 40));
            graphics.FillRectangle(brush, 10, 8, 28, 4);
            graphics.FillRectangle(brush, 18, 5, 12, 3);
            using var line = new Pen(Color.FromArgb(230, 8, 12, 18), 2);
            graphics.DrawLine(line, 20, 17, 20, 34);
            graphics.DrawLine(line, 28, 17, 28, 34);
        }
        else
        {
            graphics.FillRectangle(brush, 5, 8, 38, 25);
            using var darkBrush = new SolidBrush(Color.FromArgb(255, 8, 12, 18));
            graphics.FillRectangle(darkBrush, 9, 12, 30, 17);
            graphics.FillRectangle(brush, 22, 33, 5, 6);
            graphics.FillRectangle(brush, 15, 39, 19, 3);
        }
    }

    private static Point[] Points(params int[] coordinates)
    {
        var points = new Point[coordinates.Length / 2];
        for (var index = 0; index < points.Length; index++)
            points[index] = new Point(coordinates[index * 2], coordinates[index * 2 + 1]);

        return points;
    }
}
