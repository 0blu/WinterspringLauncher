using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WinterspringLauncher.Utils;

public static class WindowsShellApi
{
    public static string GetDesktopPath()
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        return desktopPath;
    }

    public static void CreateShortcut(string lnkPath, string description, string shortcutTarget, string workingDirectory)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new Exception("Only supported on Windows");
        
        Guid wshShellGuid = new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8");
        var wshShell = (IWshShell)Activator.CreateInstance(Marshal.GetTypeFromCLSID(wshShellGuid)!)!;
        var shortcut = (IWshShortcut)wshShell.CreateShortcut(lnkPath);

        shortcut.Description = description;
        shortcut.TargetPath = shortcutTarget;
        shortcut.WorkingDirectory = workingDirectory;

        shortcut.Save();
    }

    [Guid("41904400-BE18-11D3-A28B-00104BD35090")]
    [TypeIdentifier]
    [ComImport]
    private interface IWshShell
    {
        private extern void _VtblGap1_4();

        [DispId(1002)]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [return: MarshalAs(UnmanagedType.IDispatch)]
        public object CreateShortcut([MarshalAs(UnmanagedType.BStr)] [In] string pathLink);
    }

    [Guid("F935DC23-1CF0-11D0-ADB9-00C04FD58A0B")]
    [TypeIdentifier]
    [ComImport]
    private interface IWshShortcut
    {
        [DispId(0)]
        public string FullName { [DispId(0), MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] [return: MarshalAs(UnmanagedType.BStr)] get; }

        [DispId(1000)]
        public string Arguments { [DispId(1000), MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] [return: MarshalAs(UnmanagedType.BStr)] get; [DispId(1000), MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] [param: MarshalAs(UnmanagedType.BStr), In] set; }

        [DispId(1001)]
        public string Description { [DispId(1001), MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] [return: MarshalAs(UnmanagedType.BStr)] get; [DispId(1001), MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] [param: MarshalAs(UnmanagedType.BStr), In] set; }

        [SpecialName]
        [MethodImpl(MethodCodeType = MethodCodeType.Runtime)]
        private extern void _VtblGap1_2();

        [DispId(1003)]
        public string IconLocation { [DispId(1003), MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] [return: MarshalAs(UnmanagedType.BStr)] get; [DispId(1003), MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] [param: MarshalAs(UnmanagedType.BStr), In] set; }

        [SpecialName]
        [MethodImpl(MethodCodeType = MethodCodeType.Runtime)]
        private extern void _VtblGap2_1();

        [DispId(1005)]
        string TargetPath { [DispId(1005), MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] [return: MarshalAs(UnmanagedType.BStr)] get; [DispId(1005), MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] [param: MarshalAs(UnmanagedType.BStr), In] set; }

        [SpecialName]
        [MethodImpl(MethodCodeType = MethodCodeType.Runtime)]
        private extern void _VtblGap3_2();

        [DispId(1007)]
        public string WorkingDirectory { [DispId(1007), MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] [return: MarshalAs(UnmanagedType.BStr)] get; [DispId(1007), MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)] [param: MarshalAs(UnmanagedType.BStr), In] set; }

        [SpecialName]
        [MethodImpl(MethodCodeType = MethodCodeType.Runtime)]
        private extern void _VtblGap4_1();

        [DispId(2001)]
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public void Save();
    }
}
