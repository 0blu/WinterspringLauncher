using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using EverlookClassic.Launcher.Utils;

namespace EverlookClassic.Launcher;

class Launcher
{
    private const string CONFIG_FILE_NAME = "everlook-classic.json";
    
    internal static void Main()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

        PrintLogo();

        var baseFolder = Path.GetFullPath(".");
        Console.WriteLine($"BasePath: {baseFolder}");
        string FullPath(string subPath) => Path.GetFullPath(Path.Combine(baseFolder, subPath));

        string configPath = Path.Combine(baseFolder, CONFIG_FILE_NAME);
        var config = LoadConfig(configPath);
        var api = new UpdateApiClient(config);

        Thread.Sleep(TimeSpan.FromSeconds(1));
        IgnoreExceptions("update launcher", () => UpdateThisLauncherIfNecessary(api));

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

        bool weAreOnMacOs = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        IgnoreExceptions("prepare game", () => PrepareGame(api, weAreOnMacOs, gamePath));
        IgnoreExceptions("prepare HermesProxy", () => PrepareHermes(api, weAreOnMacOs, hermesPath));
        IgnoreExceptions("prepare ArctiumLauncher", () => PrepareArctiumLauncher(api, weAreOnMacOs, arctiumPath));

        LauncherActions.PrepareGameConfigWtf(gamePath);
        LauncherActions.StartGameViaArctium(gamePath, arctiumPath);

        string realmlist = config.Realmlist;
        LauncherActions.PrepareHermesProxyConfig(hermesPath, realmlist);
        LauncherActions.StartHermesProxyAndWaitTillEnd(hermesPath);
    }

    private static void UpdateThisLauncherIfNecessary(UpdateApiClient api)
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version!;
        string myLauncherVersion = $"{v.Major}.{v.Minor}.{v.Build}";

        GitHubReleaseInfo latestLauncherVersion = api.GetLatestThisLauncherRelease();

        if (latestLauncherVersion.TagName != null && myLauncherVersion != latestLauncherVersion.TagName)
        {
            Console.WriteLine($"New launcher update {myLauncherVersion} => {latestLauncherVersion.TagName}");
            // This function might not return because it updates the launcher in-place
            LauncherActions.UpdateThisLauncher(latestLauncherVersion);
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
                LauncherActions.DownloadGameClientZip(gameDownloadSource);
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
        WriteWithASubtext("    .,,,.  ,,,,,,,,,,,    ,,,", "      Everlook Classic");
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
}
