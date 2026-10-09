using System.Runtime.InteropServices;

namespace DeskRail.Core.Interop;

public static class DwmInterop
{
    public enum DWM_SYSTEMBACKDROP_TYPE : int
    {
        Auto = 0,
        None = 1,
        Mica = 2,
        TransientWindow = 3,
        TabbedWindow = 4
    }

    public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(
        nint hwnd, int dwAttribute,
        ref int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(
        nint hwnd, int dwAttribute,
        ref DWM_SYSTEMBACKDROP_TYPE pvAttribute, int cbAttribute);

    public static int DisableSystemBackdrop(nint hwnd)
    {
        var type = DWM_SYSTEMBACKDROP_TYPE.None;
        return DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref type, sizeof(int));
    }

    public static int EnableDarkMode(nint hwnd)
    {
        var enabled = 1;
        return DwmSetWindowAttribute(
            hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
    }
}
