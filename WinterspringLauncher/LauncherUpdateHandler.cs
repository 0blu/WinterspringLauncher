using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace WinterspringLauncher;

public static class LauncherUpdateHandler
{
    public static bool/*exitNow*/ HandleStartArguments(string[] args)
    {
        if (args.Length != 2)
            return false;

        var actionName = args[0];
        var targetPath = args[1];
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            Console.WriteLine("AutoUpdate: Target path is empty");
            return true;
        }

        switch (actionName)
        {
            case "--copy-self-to":
            {
                CreateTerminalWindowIfPossible();
                Console.WriteLine($"Updating launcher '{targetPath}'");
                var ourPath = Process.GetCurrentProcess().MainModule!.FileName!;
                bool wasSuccessful = false;
                const int maxTries = 20;
                for (int i = 0; i < maxTries; i++)
                {
                    try
                    {
                        File.Copy(ourPath, targetPath, overwrite: true);
                        wasSuccessful = true;
                    }
                    catch(IOException)
                    {
                        Console.WriteLine($"Need to wait for old process to close (this might take a bit) (try {i + 1}/{maxTries})");
                        Thread.Sleep(TimeSpan.FromMilliseconds(500));
                    }
                }

                if (!wasSuccessful)
                {
                    Console.WriteLine("Update was not successful, please try again or update manually");
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                    return true;
                }

                Console.WriteLine("Start new launcher");
                Process.Start(new ProcessStartInfo{
                    FileName = targetPath,
                    Arguments = $"--delete-tmp-updater-file \"{ourPath}\"",
                    UseShellExecute = true,
                });
                return true;
            }
            case "--delete-tmp-updater-file":
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(500));
                try
                {
                    Console.WriteLine($"Removing tmp file '{targetPath}'");
                    File.Delete(targetPath);
                }
                catch
                {
                    // Ignore
                }
                return false; // keep our current instance
            }
            default:
                return false;
        }
    }

#if PLATFORM_WINDOWS
    [DllImport("kernel32.dll")]
    static extern bool AttachConsole(int dwProcessId);
    private const int ATTACH_PARENT_PROCESS = -1;
#endif

    private static void CreateTerminalWindowIfPossible()
    {
#if PLATFORM_WINDOWS
        AttachConsole(ATTACH_PARENT_PROCESS);
#endif
    } 
}
