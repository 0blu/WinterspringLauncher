using DK.WshRuntime;

namespace EverlookClassic.Launcher;

public static class WindowsShellApi
{
    public static string GetDesktopPath()
    {
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        return desktopPath;
    }

    public static void CreateShortcut(string lnkPath, string description, string shortcutTarget)
    {
        WshInterop.CreateShortcut(lnkPath, description, shortcutTarget, "", null);
    } 
}
