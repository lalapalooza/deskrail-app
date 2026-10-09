namespace DeskRail.Core.Models;

public class DesktopItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public FileType Type { get; set; } = FileType.Other;
    public byte[]? IconSource { get; set; }
    public long Size { get; set; }
    public string SizeDisplay { get; set; } = string.Empty;
    public DateTime Modified { get; set; }
    public string Extension { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public string? ShortcutTarget { get; set; }
}
