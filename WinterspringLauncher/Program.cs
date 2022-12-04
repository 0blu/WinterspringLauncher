using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using WinterspringLauncher.Utils;

namespace WinterspringLauncher;

class Launcher
{
    private const string LEGACY_CONFIG_FILE_NAME = "everlook-classic.json";
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

        var baseFolder = Path.GetFullPath(".");
        Console.WriteLine($"BasePath: {baseFolder}");
        string FullPath(string subPath) => Path.GetFullPath(Path.Combine(baseFolder, subPath));

        string configPath = Path.Combine(baseFolder, CONFIG_FILE_NAME);
        // Convert legacy config into new one
        IgnoreExceptions("rename old config", () => {
            string legacyConfigPath = Path.Combine(baseFolder, LEGACY_CONFIG_FILE_NAME);
            if (File.Exists(legacyConfigPath))
            {
                Console.WriteLine($"Renaming old config to {CONFIG_FILE_NAME}");
                Thread.Sleep(TimeSpan.FromSeconds(1));
                File.Move(legacyConfigPath, configPath);
            }
        });

        var config = LoadConfig(configPath);
        var api = new UpdateApiClient(config);

        Thread.Sleep(TimeSpan.FromSeconds(1));
        bool weAreOnMacOs = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        IgnoreExceptions("update launcher", () => UpdateThisLauncherIfNecessary(api, weAreOnMacOs, onlyNotify: !config.AutoUpdateThisLauncher));

        if (config.RecreateDesktopShortcut)
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
        EnsureDirectoryExists(arctiumPath);

        IgnoreExceptions("prepare game", () => PrepareGame(api, weAreOnMacOs, gamePath));
        IgnoreExceptions("prepare HermesProxy", () => PrepareHermes(api, weAreOnMacOs, hermesPath));
        IgnoreExceptions("prepare ArctiumLauncher", () => PrepareArctiumLauncher(api, weAreOnMacOs, arctiumPath));

        LauncherActions.PrepareGameConfigWtf(gamePath);
        LauncherActions.StartGameViaArctium(gamePath, arctiumPath);

        string realmlist = config.Realmlist;
        LauncherActions.PrepareHermesProxyConfig(hermesPath, realmlist);
        LauncherActions.StartHermesProxyAndWaitTillEnd(hermesPath);
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
            Console.WriteLine($">>> Failed to {description}, continuing anyways <<<");
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
        var ghReleaseInfo = api.GetLatestArctiumLauncherRelease();

        var osName = weAreOnMacOs ? "mac" : "win";
        var dlSelector = (GitHubReleaseInfo.Asset a) =>
            a.Name.Contains(osName, StringComparison.CurrentCultureIgnoreCase) &&
            !a.Name.Contains("console", StringComparison.CurrentCultureIgnoreCase) &&
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

            if (!LauncherActions.ContainsValidGameZip())
            {
                var gameDownloadSource = api.GetWindowsGameDownloadSource();
                try
                {
                    LauncherActions.DownloadGameClientZip(gameDownloadSource);
                }
                catch
                {
                    Console.WriteLine("Failed to download the game client :(");
                    Console.WriteLine("Is your provider blocking the download?");
                    Console.WriteLine("Please try to download it manually");
                    Console.WriteLine(gameDownloadSource);
                    Console.WriteLine($"And place it as {LauncherActions.TMP_ARCHIVE_NAME_GAME}");
                    Console.WriteLine();
                    Console.WriteLine("Script is going to wait for 30 sec and then tries to continue (might be broken)");
                    Thread.Sleep(TimeSpan.FromSeconds(30));
                    throw;
                }
            }

            LauncherActions.ExtractGameClient(gamePath, onlyData: weAreOnMacOs);
            if (!LauncherActions.CheckGameIntegrity(gamePath, macBuild: weAreOnMacOs))
            {
                Console.WriteLine("Somehow the extraction was not successful, continuing anyways #yolo");
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
            else
            {
                LauncherActions.RemoveTempGameClientZip();
            }

            if (weAreOnMacOs)
            {
                LauncherActions.DownloadAndApplyMacOsPatches(gamePath);
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
