using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Linq;
using WinterspringLauncher.Utils;

namespace WinterspringLauncher;

class Launcher
{
    private const string CONFIG_FILE_NAME = "winterspring-launcher-config.json";
    
    internal static void Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        if (args.Length > 0)
        {
            bool exitNow = HandleLauncherUpdateArguments(args);
            if (exitNow)
            {
                return;
            }
        }

        PrintLogo();

        bool weAreOnMacOs = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        if (weAreOnMacOs)
        {
            string home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "WinterspringLauncher");
            Directory.CreateDirectory(home);
            Environment.CurrentDirectory = home;
        }
        var baseFolder = Path.GetFullPath(".");
        Console.WriteLine($"BasePath: {baseFolder}");
        string FullPath(string subPath) => Path.GetFullPath(Path.Combine(baseFolder, subPath));

        string configPath = Path.Combine(baseFolder, CONFIG_FILE_NAME);
        var config = LoadConfig(configPath);
        var api = new UpdateApiClient(config);

        Thread.Sleep(TimeSpan.FromSeconds(1));
        IgnoreExceptions("update launcher", () => UpdateThisLauncherIfNecessary(api, weAreOnMacOs, onlyNotify: !config.AutoUpdateThisLauncher));

        if (config.RecreateDesktopShortcut && !weAreOnMacOs)
        {
            LauncherActions.CreateDesktopShortcut();
            config.RecreateDesktopShortcut = false;
            config.SaveConfig(configPath); // We created a desktop shortcut
        }

        string gamePath = FullPath(config.GamePath);
        string hermesPath = FullPath(config.HermesProxyPath);
        string arctiumPath = FullPath(config.ArctiumLauncherPath);

        EnsureDirectoryExists(gamePath);
        EnsureDirectoryExists(hermesPath);
        if (!weAreOnMacOs)
            EnsureDirectoryExists(arctiumPath);

        IgnoreExceptions("prepare game", () => PrepareGame(api, weAreOnMacOs, gamePath));
        IgnoreExceptions("prepare HermesProxy", () => PrepareHermes(api, weAreOnMacOs, hermesPath));
        if (!weAreOnMacOs)
            IgnoreExceptions("prepare ArctiumLauncher", () => PrepareArctiumLauncher(api, weAreOnMacOs, arctiumPath));

        LauncherActions.PrepareGameConfigWtf(gamePath);

        IgnoreExceptions("prepare HermesProxy Data", () => LauncherActions.PrepareHermesProxyData(hermesPath));
        LauncherActions.PrepareHermesProxyConfig(hermesPath, config.Realmlist);

        Process? wowProcess;
        if (weAreOnMacOs)
            wowProcess = LauncherActions.StartPatchedGameDirectly(gamePath, weAreOnMacOs);
        else
            wowProcess = LauncherActions.StartGameViaArctium(gamePath, arctiumPath);

        if (wowProcess == null || wowProcess.HasExited)
        {
            ColorConsole.Yellow("WoW did not start correctly");
            ColorConsole.Yellow("Trying to start HermesProxy anyways. Maybe you find a way how to start WoW.");
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }

        Process hermesProcess = LauncherActions.StartHermesProxyAndWaitTillEnd(hermesPath);

        // Wait for one of the processes to close
        if (wowProcess != null)
            Task.WaitAny(wowProcess.WaitForExitAsync(), hermesProcess.WaitForExitAsync());
        else
            Task.WaitAny(hermesProcess.WaitForExitAsync());

        Thread.Sleep(TimeSpan.FromSeconds(1));
        if (wowProcess?.HasExited ?? false)
        {
            ColorConsole.Yellow("-------------");
            ColorConsole.Yellow("World of Warcraft was closed");
            ColorConsole.Yellow("You can close this window");
            ColorConsole.Yellow("-------------");
        }
        else if (hermesProcess.HasExited)
        {
            ColorConsole.Yellow("Hermes Proxy has exited?");
            Console.WriteLine("Please report any errors to https://github.com/WowLegacyCore/HermesProxy");
            Thread.Sleep(TimeSpan.FromSeconds(5));
            Console.WriteLine("Press any enter to close this window");
        }
        else
        {
            ColorConsole.Yellow("Something closed unexpectedly?");
        }
        Console.ReadLine();
    }

    private static void UpdateThisLauncherIfNecessary(UpdateApiClient api, bool weAreOnMacOs, bool onlyNotify)
    {
        Version myVersion = Assembly.GetExecutingAssembly().GetName().Version!;
        string myVersionStr = $"{myVersion.Major}.{myVersion.Minor}.{myVersion.Build}";

        GitHubReleaseInfo latestLauncherVersion = api.GetLatestThisLauncherRelease();
        if (latestLauncherVersion.TagName != null && myVersionStr != latestLauncherVersion.TagName)
        {
            var newVersion = Version.Parse(latestLauncherVersion.TagName);
            if (newVersion > myVersion)
            {
                Console.WriteLine($"New launcher update {myVersionStr} => {latestLauncherVersion.TagName}");
                if (onlyNotify)
                {
                    Console.WriteLine("A new version was released, please update");
                    Console.WriteLine("https://github.com/0blu/WinterspringLauncher/releases");
                    Thread.Sleep(TimeSpan.FromSeconds(12));
                }
                else
                {
                    // This function might not return because it updates the launcher in-place
                    LauncherActions.UpdateThisLauncher(weAreOnMacOs, latestLauncherVersion);
                }
            }
        }
    }

    private static void IgnoreExceptions(string description, Action func)
    {
#if DEBUG
        func();
#else
        try
        {
            func();
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
            ColorConsole.Yellow($">>> Failed to {description}, continuing anyways (might not work) <<<");
            Thread.Sleep(TimeSpan.FromSeconds(10));
        }
#endif
    }

    private static void PrepareHermes(UpdateApiClient api, bool weAreOnMacOs, string hermesPath)
    {
        var ghReleaseInfo = api.GetLatestHermesProxyRelease();

        var osName = weAreOnMacOs ? "mac" : "win";
        var dlSelector = (GitHubReleaseInfo.Asset a) =>
            a.Name.Contains(osName, StringComparison.CurrentCultureIgnoreCase);

        ExecuteGithubDownloadIfUpdateAvailable("HermesProxy", hermesPath, ghReleaseInfo, dlSelector, LauncherActions.ClearAndDownloadHermesProxy);
    }

    private static void PrepareArctiumLauncher(UpdateApiClient api, bool weAreOnMacOs, string arctiumPath)
    {
        if (weAreOnMacOs)
            throw new Exception("Arctium does not support MacOS");
        
        var ghReleaseInfo = api.GetLatestArctiumLauncherRelease();

        var dlSelector = (GitHubReleaseInfo.Asset a) =>
            a.Name.Contains("win", StringComparison.CurrentCultureIgnoreCase) &&
            !a.Name.Contains("noConsole", StringComparison.CurrentCultureIgnoreCase) &&
            !a.Name.Contains("mods", StringComparison.CurrentCultureIgnoreCase);

        ExecuteGithubDownloadIfUpdateAvailable("ArctiumLauncher", arctiumPath, ghReleaseInfo, dlSelector, LauncherActions.ClearAndDownloadArctiumLauncher);
    }

    private static void ExecuteGithubDownloadIfUpdateAvailable(string description, string modulePath, GitHubReleaseInfo ghReleaseInfo, Func<GitHubReleaseInfo.Asset, bool> assetSelector, Action<string, string> downloader)
    {
        var localVersion = LocalVersion.GetLocalVersion(modulePath);
        if (ghReleaseInfo.TagName == null)
        {
            Console.WriteLine($"Unable to fetch latest {description} release from GitHub");
            if (localVersion == null)
            {
                throw new Exception($"No local {description} available");
            }
        }
        else
        {
            var versionString = $"{ghReleaseInfo.TagName}|{ghReleaseInfo.Name}";
            if (localVersion != versionString)
            {
                Console.WriteLine($"Found newer version of {description}, upgrading from {localVersion ?? "<null>"} => {versionString}");

                var dlUrl = ghReleaseInfo.Assets!.Single(assetSelector).DownloadUrl;

                Console.WriteLine($"Downloading {description}");
                downloader(dlUrl, modulePath);
                LocalVersion.WriteLocalVersion(modulePath, versionString, dlUrl);
            }
        }
    }

    private static LauncherConfig LoadConfig(string configPath)
    {
        try
        {
            var config = LauncherConfig.LoadOrCreateDefault(configPath);
            return config;
        }
        catch(Exception e)
        {
            Console.WriteLine(e);
            Console.WriteLine("");
            Console.WriteLine("Failed to load config, remove 'launcher.json' if you want to restore defaults");

            Thread.Sleep(TimeSpan.FromSeconds(10));
            Environment.Exit(1);
            throw;
        }
    }

    private static void PrepareGame(UpdateApiClient api, bool weAreOnMacOs, string gamePath)
    {
        if (!LauncherActions.CheckGameIntegrity(gamePath, macBuild: weAreOnMacOs))
        {
            Console.WriteLine("Did not found complete 1.14 game installation");

            if (!LauncherActions.ContainsValidGameZip(weAreOnMacOs))
            {
                var gameDownloadSource = weAreOnMacOs
                    ? api.GetMacGamePatchDownloadSource()
                    : api.GetWindowsGameDownloadSource();
                try
                {
                    LauncherActions.DownloadGameClientZip(gameDownloadSource);
                }
                catch
                {
                    ColorConsole.Yellow("Failed to download the game client :(");
                    Console.WriteLine("Is your provider blocking the download?");
                    Console.WriteLine("Please try to download it manually");
                    Console.WriteLine(gameDownloadSource);
                    Console.WriteLine($"And place it as {LauncherActions.TmpGameArchiveName}");
                    Console.WriteLine();
                    Console.WriteLine("Script is going to wait for 30 sec and then tries to continue (might be broken)");
                    Thread.Sleep(TimeSpan.FromSeconds(30));
                    throw;
                }
            }

            LauncherActions.ExtractGameClient(gamePath);
            if (weAreOnMacOs)
            {
                var wtfConfig = Path.Combine(gamePath, "_classic_era_", "WTF", "Config.wtf");
                File.AppendAllLines(wtfConfig, new []{ "SET cursorSizePreferred \"0\"" }); // fix huge cursor on macos
            }

            if (weAreOnMacOs)
            {
                var patcherUrl = api.GetGamePatchingServiceUrl();
                LauncherActions.PatchGameClient(gamePath, macBuild: weAreOnMacOs, patcherUrl);
            }
            if (!LauncherActions.CheckGameIntegrity(gamePath, macBuild: weAreOnMacOs))
            {
                ColorConsole.Yellow("Somehow the extraction was not successful, continuing anyways #yolo");
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }
            else
            {
                LauncherActions.RemoveTempGameClientZip();
            }
        }
        else
        {
            Console.WriteLine($"Found client{(weAreOnMacOs ? " (mac)" : "")} in {gamePath}");
        };
    }

    private static void EnsureDirectoryExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);
    }

    private static bool IsUpdateRequired(string? myVersion, string? latestAvailableVersion)
    {
#if DEBUG
        return false;
#else
        // Only update if we dont have a local version
        // or the latest available version is not the same as ours
        return myVersion == null || (latestAvailableVersion != null && myVersion != latestAvailableVersion);
#endif
    }

    private static string GetVersionInformation()
    {
        string version = $"{GitVersionInformation.CommitDate} {GitVersionInformation.MajorMinorPatch}";
        if (GitVersionInformation.CommitsSinceVersionSource != "0")
            version += $"+{GitVersionInformation.CommitsSinceVersionSource}({GitVersionInformation.ShortSha})";
        if (GitVersionInformation.UncommittedChanges != "0")
            version += " dirty";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            version += " (MacOS)";
        return version;
    }

    private static void PrintLogo()
    {
        Console.WriteLine($"Version: {GetVersionInformation()}");
        Console.WriteLine("https://github.com/0blu/WinterspringLauncher");
        Console.WriteLine("");

        void WriteWithASubtext(string logo, string subText)
        {
            Console.Write(logo);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(subText);
            Console.ForegroundColor = ConsoleColor.Magenta;
        }

        var pre = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("            ...      ..");
        Console.WriteLine("       .,,,,,..        .,,");
        Console.WriteLine("     ,,,,,    ....       ,,,");
        WriteWithASubtext("    .,,,.  ,,,,,,,,,,,    ,,,", "        Winterspring");
        WriteWithASubtext("    ,,,,. .,,,,   ,,,,.   ,,,,", "         Launcher");
        Console.WriteLine("    .,,,,         ,,,,.   ,,,,");
        WriteWithASubtext("     ,,,,,,,...,,,,,,,   ,,,,.", "      Allows you to");
        WriteWithASubtext("       ,,,,,,,,,,,..   .,,,,,", "      play on Everlook");
        WriteWithASubtext("   ,.        .       .,,,,,,", "    using the modern client");
        Console.WriteLine("     ,,,..      ..,,,,,,,.");
        Console.WriteLine("       .,,,,,,,,,,,,,,,");
        Console.ForegroundColor = pre;
        Console.WriteLine("");
    }

    private static bool/*exitNow*/ HandleLauncherUpdateArguments(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine($"AutoUpdate: Error invalid arguments [{string.Join(", ", args.Select(x => $"\"{x}\""))}]");
            return true;
        }

        var actionName = args[0];
        var targetPath = args[1];
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            Console.WriteLine("AutoUpdate: Target path is empty");
            return true;
        }

        // Launcher update order:
        // - launcher detects an update
        // - downloads update into tmp file
        // - starts tmp file with "--copy-self-to <launcherPath>"
        // - launcher kills itself
        // - tmp file will copy itself to launcher
        // - tmp file will start launcher with "--delete-tmp-updater-file <tmpFile>"
        // - launcher tries to delete tmpFile and does a regular start
        if (actionName == "--copy-self-to")
        {
            Console.WriteLine($"Updating launcher '{targetPath}'");
            var ourPath = Process.GetCurrentProcess().MainModule!.FileName!;
            bool wasSuccessful = false;
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    File.Copy(ourPath, targetPath, overwrite: true);
                    wasSuccessful = true;
                }
                catch(IOException)
                {
                    Console.WriteLine("Need to wait for old process to close (this might take a bit)");
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
        }
        else if (actionName == "--delete-tmp-updater-file")
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
            return false;
        }
        else
        {
            Console.WriteLine($"AutoUpdate: Unknown action as arguments [{string.Join(", ", args.Select(x => $"\"{x}\""))}]");
        }
        return true;
    }
}
