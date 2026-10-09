namespace DeskRail.Core.Models;

public static class FileTypeInfo
{
    private static readonly Dictionary<string, FileType> ExtensionMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Documents
        [".doc"] = FileType.Document, [".docx"] = FileType.Document,
        [".xls"] = FileType.Document, [".xlsx"] = FileType.Document,
        [".ppt"] = FileType.Document, [".pptx"] = FileType.Document,
        [".pdf"] = FileType.Document, [".txt"] = FileType.Document,
        [".rtf"] = FileType.Document, [".odt"] = FileType.Document,
        // Images
        [".jpg"] = FileType.Image, [".jpeg"] = FileType.Image,
        [".png"] = FileType.Image, [".gif"] = FileType.Image,
        [".bmp"] = FileType.Image, [".svg"] = FileType.Image,
        [".ico"] = FileType.Image, [".webp"] = FileType.Image,
        // Music
        [".mp3"] = FileType.Music, [".flac"] = FileType.Music,
        [".wav"] = FileType.Music, [".aac"] = FileType.Music,
        [".ogg"] = FileType.Music, [".m4a"] = FileType.Music,
        [".m3u"] = FileType.Music, [".m3u8"] = FileType.Music,
        // Video
        [".mp4"] = FileType.Video, [".mkv"] = FileType.Video,
        [".avi"] = FileType.Video, [".mov"] = FileType.Video,
        [".wmv"] = FileType.Video, [".flv"] = FileType.Video,
        // Archives
        [".zip"] = FileType.Archive, [".rar"] = FileType.Archive,
        [".7z"] = FileType.Archive, [".tar"] = FileType.Archive,
        [".gz"] = FileType.Archive,
        // Code
        [".cs"] = FileType.Code, [".js"] = FileType.Code,
        [".ts"] = FileType.Code, [".py"] = FileType.Code,
        [".java"] = FileType.Code, [".cpp"] = FileType.Code,
        [".c"] = FileType.Code, [".h"] = FileType.Code,
        [".html"] = FileType.Code, [".css"] = FileType.Code,
        [".json"] = FileType.Code, [".xml"] = FileType.Code,
        [".md"] = FileType.Document, [".env"] = FileType.Code,
        [".go"] = FileType.Code,
        [".rs"] = FileType.Code, [".sh"] = FileType.Code,
    };

    public static FileType MapExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return FileType.Other;

        return ExtensionMap.TryGetValue(extension, out var type) ? type : FileType.Other;
    }
}
