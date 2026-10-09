using Microsoft.Win32;
using DeskRail.Core.Interop;

namespace DeskRail.Core.Services;

public class RegistryBridge
{
    private const string RegPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";

    private const string CLSID_ThisPC = "{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    private const string CLSID_RecycleBin = "{645FF040-5081-101B-9F08-00AA002F954E}";

    public void HideDesktopIcons()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegPath);
        key.SetValue(CLSID_ThisPC, 1, RegistryValueKind.DWord);
        key.SetValue(CLSID_RecycleBin, 1, RegistryValueKind.DWord);
        RefreshDesktop();
    }

    public void ShowDesktopIcons()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegPath);
        key.SetValue(CLSID_ThisPC, 0, RegistryValueKind.DWord);
        key.SetValue(CLSID_RecycleBin, 0, RegistryValueKind.DWord);
        RefreshDesktop();
    }

    private void RefreshDesktop()
    {
        NativeMethods.SendMessageTimeout(
            (nint)NativeMethods.HWND_BROADCAST,
            NativeMethods.WM_SETTINGCHANGE,
            nint.Zero,
            "Desktop",
            NativeMethods.SMTO_ABORTIFHUNG,
            5000,
            out _);
    }

    public void SetAutoStart(bool enabled, string exePath)
    {
        const string runPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        using var key = Registry.CurrentUser.CreateSubKey(runPath);
        if (enabled)
            key.SetValue("DeskRail", $"\"{exePath}\"", RegistryValueKind.String);
        else
            key.DeleteValue("DeskRail", false);
    }
}
