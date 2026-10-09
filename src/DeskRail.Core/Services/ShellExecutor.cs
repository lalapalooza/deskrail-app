using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DeskRail.Core.Services;

public class ShellExecutor
{
    private const int SW_SHOWNORMAL = 1;
    private const uint SEE_MASK_INVOKEIDLIST = 0x0000000C;
    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;
    private const uint CMF_NORMAL = 0x00000000;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint CMIC_MASK_UNICODE = 0x00004000;
    private const uint CMIC_MASK_PTINVOKE = 0x20000000;
    private const uint SW_SHOWNORMAL_U = 1;

    private static readonly Guid ShellFolderIid = new("000214E6-0000-0000-C000-000000000046");
    private static readonly Guid ShellContextMenuIid = new("000214E4-0000-0000-C000-000000000046");

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHELLEXECUTEINFO
    {
        public int cbSize;
        public uint fMask;
        public nint hwnd;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpVerb;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpParameters;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpDirectory;
        public int nShow;
        public nint hInstApp;
        public nint lpIDList;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpClass;
        public nint hkeyClass;
        public uint dwHotKey;
        public nint hIcon;
        public nint hProcess;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public nint hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public nint hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CMINVOKECOMMANDINFOEX
    {
        public int cbSize;
        public uint fMask;
        public nint hwnd;
        public nint lpVerb;
        public nint lpParameters;
        public nint lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public nint hIcon;
        public nint lpTitle;
        public nint lpVerbW;
        public nint lpParametersW;
        public nint lpDirectoryW;
        public nint lpTitleW;
        public POINT ptInvoke;
    }

    [ComImport]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder
    {
        [PreserveSig]
        int ParseDisplayName(nint hwnd, nint bindContext,
            [MarshalAs(UnmanagedType.LPWStr)] string displayName, out uint eaten,
            out nint itemIdList, ref uint attributes);

        [PreserveSig]
        int EnumObjects(nint hwnd, uint flags, out nint enumIdList);

        [PreserveSig]
        int BindToObject(nint itemIdList, nint bindContext, ref Guid interfaceId, out nint result);

        [PreserveSig]
        int BindToStorage(nint itemIdList, nint bindContext, ref Guid interfaceId, out nint result);

        [PreserveSig]
        int CompareIds(nint parameter, nint itemIdList1, nint itemIdList2);

        [PreserveSig]
        int CreateViewObject(nint hwnd, ref Guid interfaceId, out nint result);

        [PreserveSig]
        int GetAttributesOf(uint itemCount, ref nint itemIdList, ref uint attributes);

        [PreserveSig]
        int GetUIObjectOf(nint hwnd, uint itemCount, ref nint itemIdList,
            ref Guid interfaceId, nint reserved, out IContextMenu contextMenu);

        [PreserveSig]
        int GetDisplayNameOf(nint itemIdList, uint flags, nint name);

        [PreserveSig]
        int SetNameOf(nint hwnd, nint itemIdList,
            [MarshalAs(UnmanagedType.LPWStr)] string name, uint flags, out nint renamedItemIdList);
    }

    [ComImport]
    [Guid("000214E4-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(nint menu, uint indexMenu, uint idCommandFirst,
            uint idCommandLast, uint flags);

        [PreserveSig]
        int InvokeCommand(ref CMINVOKECOMMANDINFOEX commandInfo);

        [PreserveSig]
        int GetCommandString(nuint commandOffset, uint flags, nint reserved,
            nint commandString, uint bufferLength);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO lpExecInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHParseDisplayName(string name, nint bindContext,
        out nint itemIdList, uint attributesIn, out uint attributesOut);

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHBindToParent(nint itemIdList, ref Guid interfaceId,
        out IShellFolder parent, out nint childItemIdList);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(nint menu);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenuEx(nint menu, uint flags, int x, int y,
        nint hwnd, nint parameters);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hwnd);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(nint memory);

    public void ShowContextMenu(string path, nint ownerHwnd)
    {
        nint itemIdList = nint.Zero;
        nint menu = nint.Zero;
        IShellFolder? parent = null;
        IContextMenu? contextMenu = null;

        try
        {
            var parseResult = SHParseDisplayName(path, nint.Zero, out itemIdList, 0, out _);
            Marshal.ThrowExceptionForHR(parseResult);

            var shellFolderIid = ShellFolderIid;
            var bindResult = SHBindToParent(itemIdList, ref shellFolderIid, out parent, out var childItemIdList);
            Marshal.ThrowExceptionForHR(bindResult);

            var contextMenuIid = ShellContextMenuIid;
            var menuResult = parent.GetUIObjectOf(ownerHwnd, 1, ref childItemIdList,
                ref contextMenuIid, nint.Zero, out contextMenu);
            Marshal.ThrowExceptionForHR(menuResult);

            menu = CreatePopupMenu();
            if (menu == nint.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法创建 Shell 右键菜单。");

            var queryResult = contextMenu.QueryContextMenu(menu, 0, 1, 0x7FFF, CMF_NORMAL);
            Marshal.ThrowExceptionForHR(queryResult);

            if (!GetCursorPos(out var point))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法获取右键菜单位置。");

            SetForegroundWindow(ownerHwnd);
            var commandId = TrackPopupMenuEx(
                menu, TPM_RIGHTBUTTON | TPM_RETURNCMD, point.X, point.Y, ownerHwnd, nint.Zero);
            if (commandId == 0)
                return;

            var invokeInfo = new CMINVOKECOMMANDINFOEX
            {
                cbSize = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>(),
                fMask = CMIC_MASK_UNICODE | CMIC_MASK_PTINVOKE,
                hwnd = ownerHwnd,
                lpVerb = (nint)(commandId - 1),
                lpVerbW = (nint)(commandId - 1),
                nShow = (int)SW_SHOWNORMAL_U,
                ptInvoke = point
            };
            var invokeResult = contextMenu.InvokeCommand(ref invokeInfo);
            Marshal.ThrowExceptionForHR(invokeResult);
        }
        finally
        {
            if (menu != nint.Zero)
                DestroyMenu(menu);
            if (contextMenu is not null && Marshal.IsComObject(contextMenu))
                Marshal.ReleaseComObject(contextMenu);
            if (parent is not null && Marshal.IsComObject(parent))
                Marshal.ReleaseComObject(parent);
            if (itemIdList != nint.Zero)
                CoTaskMemFree(itemIdList);
        }
    }

    public void Open(string path)
    {
        var psi = new ProcessStartInfo(path) { UseShellExecute = true };
        Process.Start(psi);
    }

    public void OpenInExplorer(string path)
    {
        Process.Start("explorer.exe", $"/select,\"{path}\"");
    }

    public void ShowProperties(string path)
    {
        var sei = new SHELLEXECUTEINFO
        {
            cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>(),
            fMask = SEE_MASK_INVOKEIDLIST,
            lpVerb = "properties",
            lpFile = path,
            nShow = SW_SHOWNORMAL,
        };
        ShellExecuteEx(ref sei);
    }

    public void DeleteToRecycleBin(string path)
    {
        var op = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = path + "\0",
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT,
        };
        SHFileOperation(ref op);
    }
}
