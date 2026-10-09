using System.IO;
using System.Text.Json;

namespace DeskRail.Core.Services;

public static class LockFile
{
    private static readonly string LockPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DeskRail", "deskrail.lock");

    public static bool Exists() => File.Exists(LockPath);

    public static void Create()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LockPath)!);
        File.WriteAllText(LockPath, DateTime.Now.ToString("O"));
    }

    public static void Delete()
    {
        if (File.Exists(LockPath))
            File.Delete(LockPath);
    }
}
