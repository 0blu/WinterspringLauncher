using Avalonia;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.Logging;

namespace WinterspringLauncher;

class ProgramStartup
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        bool weAreOnMacOs = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        if (weAreOnMacOs)
        {
            string home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "WinterspringLauncher");
            Directory.CreateDirectory(home);
            Environment.CurrentDirectory = home;
        }
        else
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(AppContext.BaseDirectory)!;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
        
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace(LogEventLevel.Verbose);
}
