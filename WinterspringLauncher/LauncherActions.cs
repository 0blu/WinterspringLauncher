using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Xml;
using WinterspringLauncher.Utils;

namespace WinterspringLauncher;

public static class LauncherActions
{
    public static string TmpGameArchiveName => RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? "__tmp__game-client.zip"
        : "__tmp__game-client.rar";

    public static void UpdateThisLauncher(bool weAreOnMacOs, GitHubReleaseInfo releaseInfo)
    {
        if (weAreOnMacOs)
            throw new Exception("Autoupdate of the launcher is not yet supported on MacOS");

        // C# can only create temp files with a fixed suffix, but we might want to have ".exe" at the end
        var rawTempFilePath = Path.GetTempFileName();
        try { File.Delete(rawTempFilePath); } catch {/*ignore*/}
        var tempFilePath = Path.ChangeExtension(rawTempFilePath, weAreOnMacOs ? "WinterspringLauncher" : "WinterspringLauncher.exe");

        var selectedAsset = releaseInfo.Assets!.Single(a => a.Name.Contains("WinterspringLauncher") && (a.Name.Contains(".exe") != weAreOnMacOs));
        var downloadUrl = selectedAsset.DownloadUrl;

        Console.WriteLine($"Downloading new version: {downloadUrl}");
        Console.WriteLine($"Into file: {tempFilePath}");

        DownloadFile(downloadUrl, tempFilePath);

        Console.WriteLine("Going to start tmp file in copy mode to replace the following path");
        var ourPath = Process.GetCurrentProcess().MainModule!.FileName!;
        Console.WriteLine($"Replace: '{ourPath}'");
        Thread.Sleep(TimeSpan.FromSeconds(3));
        var process = Process.Start(new ProcessStartInfo{
            FileName = tempFilePath,
            Arguments = $"--copy-self-to \"{ourPath}\"",
            UseShellExecute = true,
        });
        Thread.Sleep(TimeSpan.FromMilliseconds(200));
        if (process == null || process.HasExited)
        {
            throw new Exception("The tmp updater has exited prematurely");
        }

        Environment.Exit(0);
    }

    public static bool CheckGameIntegrity(string gamePath, bool macBuild)
    {
        string filePath = GetGameExecutableFilePath(gamePath, macBuild, patched: macBuild);
        var wowClassicExe = new FileInfo(filePath);
        if (!wowClassicExe.Exists)
            return false;

        // Allow custom clients by just checking the size and not the checksum
        return wowClassicExe.Length > (25 * 1024 * 1024); // should be more than 25 MiB
    }

    public static void CreateDesktopShortcut()
    {
        try
        {
            string linkName = "Play on Everlook";
            string description = "Click here to play WoW on Everlook.org";

            var desktopPath = WindowsShellApi.GetDesktopPath();
            string shortcutPath = Path.Combine(desktopPath, $"{linkName}.lnk");
            if (File.Exists(shortcutPath))
            {
                Console.WriteLine("Replacing existing Desktop Icon");
            }

            string? target = Process.GetCurrentProcess().MainModule?.FileName;
            if (target == null)
            {
                return; // I dont know where I am
            }

            var workingDirectory = Path.GetDirectoryName(target)!;

            Console.WriteLine("Creating Desktop Icon");
            WindowsShellApi.CreateShortcut(shortcutPath, description, target, workingDirectory);
        }
        catch
        {
            // Ignore
        }
    }

    private static void DownloadFile(string url, string outFilename)
    {
        using (var client = new FileDownloader(url, outFilename))
        {
            client.InitialInfo += (totalFileSize) => {
                Console.WriteLine($"Download size {UtilHelper.ToHumanFileSize(totalFileSize ?? -1)}");
            };

            ProgressBarPrinter progressBar = new ProgressBarPrinter("Download");
            client.ProgressChangedFixedDelay += (totalFileSize, totalBytesDownloaded, bytePerSec) => {
                double progress = ((double)totalBytesDownloaded) / (totalFileSize ?? 1);
                string dataRatePerSec = UtilHelper.ToHumanFileSize(bytePerSec);
                progressBar.UpdateState(progress, $"{dataRatePerSec}/s".PadRight(11));
            };

            client.DownloadDone += () => {
                progressBar.Done();
            };

            client.StartGetDownload().Wait();
        }
    }

    private static string CreateMd5Checksum(string filePath)
    {
        using (var stream = File.OpenRead(filePath))
        {
            using (var md5 = MD5.Create())
            {
                md5.ComputeHash(stream);
                return BitConverter.ToString(md5.Hash!).Replace("-", String.Empty).ToLower();
            }
        }
    }

    public static void PrepareGameConfigWtf(string gamePath)
    {
        var configWtfPath = Path.Combine(gamePath, "_classic_era_", "WTF", "Config.wtf");
        var dirName = Path.GetDirectoryName(configWtfPath);
        Directory.CreateDirectory(dirName!);

        List<string> configContent;
        if (File.Exists(configWtfPath))
            configContent = File.ReadAllLines(configWtfPath).ToList();
        else
            configContent = new List<string>();

        var newLine = "SET portal \"127.0.0.1:1119\"";
        bool wasChanged = false;

        var currentPortalLine = configContent.FindIndex(l => l.StartsWith("SET portal "));
        if (currentPortalLine != -1)
        {
            if (configContent[currentPortalLine] != newLine)
            {
                configContent[currentPortalLine] = newLine;
                wasChanged = true;
            }
        }
        else
        {
            configContent.Add(newLine);
            wasChanged = true;
        }

        if (wasChanged)
        {
            File.WriteAllLines(configWtfPath, configContent, Encoding.UTF8);
        }
    }

    public static Process? StartPatchedGameDirectly(string gamePath, bool weAreOnMacOs)
    {
        var executablePath = GetGameExecutableFilePath(gamePath, weAreOnMacOs, true);

        Console.WriteLine("Starting WoW...");

        ProcessStartInfo startInfo;
        if (weAreOnMacOs)
        {
            startInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/open",
                ArgumentList = { "--new", "--wait-apps", "./WoW Fixed.app" },
                WorkingDirectory = Path.Combine(gamePath, "_classic_era_"),
                UseShellExecute = true,
                CreateNoWindow = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = true,
                CreateNoWindow = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
            };
        }

        startInfo.EnvironmentVariables.Clear();
        var process = Process.Start(startInfo)!;

        Thread.Sleep(TimeSpan.FromSeconds(2.5));
        if (process.HasExited)
        {
            ColorConsole.Yellow("Error: Somehow the game exited prematurely");
        }

        return process;
    }

    public static Process? StartGameViaArctium(string gamePath, string arctiumPath)
    {
        const string wowProcName = "WowClassic";
        var arctiumExePath = Path.Combine(arctiumPath, "Arctium WoW Launcher.exe");

        Console.WriteLine("Starting WoW...");

        var preStartIds = Process.GetProcessesByName(wowProcName).Select(p => p.Id);
        var arctiumProc = Process.Start(new ProcessStartInfo{
            FileName = arctiumExePath,
            WorkingDirectory = arctiumPath,
            Arguments = $"--staticseed --version=ClassicEra --path \"{Path.Combine(gamePath, "_classic_era_")}\"",
            UseShellExecute = true,
        })!;

        Thread.Sleep(TimeSpan.FromSeconds(1));
        if (arctiumProc.HasExited)
        {
            ColorConsole.Yellow("Error: Somehow ArctiumLauncher exited prematurely");
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }

        if (!arctiumProc.WaitForExit(40_000))
        {
            ColorConsole.Yellow("Error: ArctiumLauncher timed out (?)");
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }

        var postStart = Process.GetProcessesByName(wowProcName).ToHashSet();
        postStart.RemoveWhere(p => preStartIds.Contains(p.Id));

        return postStart.LastOrDefault();
    }

    public static void PatchGameClient(string gamePath, bool macBuild, string patcherUrl)
    {
        if (!macBuild)
            throw new Exception("Patching is currently only supported on MacOS");

        UtilHelper.CopyFolderRecursively(
            srcFolder: Path.Combine(gamePath, "_classic_era_", "World of Warcraft Classic.app"),
            dstFolder: Path.Combine(gamePath, "_classic_era_", "WoW Fixed.app"),
            (file) => !file.Contains("Contents/MacOS/World of Warcraft Classic") && !file.Contains("Contents/_CodeSignature"));

        var plistPath = Path.Combine(gamePath, "_classic_era_", "WoW Fixed.app", "Contents", "info.plist");
        var plistContent = File.ReadAllText(plistPath);
        var newPlistContent = plistContent
            .Replace("CFBundleSupportedPlatforms", "CFBundleSupportedPlatformsLegacy")
            .Replace("World of Warcraft Classic", "wow_fixed");
        File.WriteAllText(plistPath, newPlistContent);

        var originalExecutablePath = GetGameExecutableFilePath(gamePath, macBuild, patched: false);
        var patchedExecutablePath = GetGameExecutableFilePath(gamePath, macBuild, patched: macBuild);

        Directory.CreateDirectory(Path.GetDirectoryName(patchedExecutablePath)!);
        Console.WriteLine("Patching game client to allow custom connections");
        Console.WriteLine("This process should not take >5 min.");
        ColorConsole.Yellow("If the estimated time is extremely high, try to restart the launcher");

        var url = new UriBuilder(patcherUrl);
        var query = HttpUtility.ParseQueryString(url.Query);
        query.Add("static-seed", "true");
        url.Query = query.ToString();
        using var downloader = new FileDownloader(url.ToString(), patchedExecutablePath);

        using var fileStream = File.OpenRead(originalExecutablePath);

        downloader.InitialInfo += (totalFileSize) => {
            Console.WriteLine($"Download size {UtilHelper.ToHumanFileSize(totalFileSize ?? -1)}");
        };

        ProgressBarPrinter progressBar = new ProgressBarPrinter("Download");
        downloader.ProgressChangedFixedDelay += (totalFileSize, totalBytesDownloaded, bytePerSec) => {
            double progress = ((double)totalBytesDownloaded) / (totalFileSize ?? 1);
            string dataRatePerSec = UtilHelper.ToHumanFileSize(bytePerSec);
            progressBar.UpdateState(progress, $"{dataRatePerSec}/s".PadRight(11));
        };

        downloader.DownloadDone += () => {
            progressBar.Done();
        };

        downloader.StartPostUploadFileAndDownload(fileStream).Wait();
        UnixApi.chmod(patchedExecutablePath, UnixApi.PERM_0777);
    }

    private static string GetGameExecutableFilePath(string gamePath, bool macBuild, bool patched)
    {
        return (macBuild, patched) switch
        {
            (false, false) => Path.Combine(gamePath, "_classic_era_", "WowClassic.exe"),
            (false, true) => Path.Combine(gamePath, "_classic_era_", "WowClassic_patched.exe"),
            (true, false) => Path.Combine(gamePath, "_classic_era_", "World of Warcraft Classic.app", "Contents", "MacOS", "World of Warcraft Classic"),
            (true, true) => Path.Combine(gamePath, "_classic_era_", "WoW Fixed.app", "Contents", "MacOS", "wow_fixed"),
        };
    }

    public static Process StartHermesProxyAndWaitTillEnd(string hermesPath)
    {
        bool weAreOnMacOs = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        var executableName = weAreOnMacOs
            ? "HermesProxy"
            : "HermesProxy.exe";

        var executablePath = Path.Combine(hermesPath, executableName);
        
        Console.WriteLine("Starting HermesProxy...");
        var process = Process.Start(new ProcessStartInfo{
            FileName = executablePath,
            WorkingDirectory = hermesPath,
            Arguments = "--no-version-check",
        })!;

        return process;
    }

    public static bool ContainsValidGameZip(bool weAreOnMacOs)
    {
        string downloadedFile = Path.Combine(TmpGameArchiveName);
        if (!File.Exists(downloadedFile))
            return false;

        long existingFileLength = new FileInfo(downloadedFile).Length;
        long expectedFileLength = weAreOnMacOs ? 8611510296 : 8004342849;
        return existingFileLength == expectedFileLength;
    }

    public static void DownloadGameClientZip((string? provider, string downloadUrl) downloadSource)
    {
        if (downloadSource.provider != null)
            Console.WriteLine($"Downloading from {downloadSource.provider}: url \"{downloadSource.downloadUrl}\"");
        else
            Console.WriteLine($"Download url \"{downloadSource.downloadUrl}\"");

        Console.WriteLine("Placing temporary download file alongside launcher");
        Thread.Sleep(TimeSpan.FromSeconds(1));
        DownloadFile(downloadSource.downloadUrl, Path.Combine(TmpGameArchiveName));
    }

    public static void ExtractGameClient(string gamePath)
    {
        ArchiveCompression.DecompressWithProgress(TmpGameArchiveName, gamePath);
    }

    public static void RemoveTempGameClientZip()
    {
        string downloadedFile = Path.Combine(TmpGameArchiveName);

        Console.WriteLine("Removing temporary archive");
        Thread.Sleep(TimeSpan.FromSeconds(2));
#if DEBUG
        Console.WriteLine($">>>DEBUG<<< Does not remove {downloadedFile}");
#else
        File.Delete(downloadedFile);
#endif
    }

    public static void ClearAndDownloadHermesProxy(string downloadUrl, string hermesPath)
    {
        var files = Directory.GetFiles(hermesPath);
        foreach (string file in files)
        {
            File.Delete(file);
        }

        var directories = Directory.GetDirectories(hermesPath);
        foreach (string directory in directories)
        {
            if (!directory.Contains("AccountData")) // we want to keep our AccountData
                Directory.Delete(directory, true);
        }


        var tempFilePath = "__tmp__hermes.zip";
        DownloadFile(downloadUrl, tempFilePath);

        ArchiveCompression.DecompressSmartSkipFirstFolder(tempFilePath, hermesPath);
        File.Delete(tempFilePath);
    }

    public static void ClearAndDownloadArctiumLauncher(string downloadUrl, string arctiumPath)
    {
        Directory.Delete(arctiumPath, true);

        var tempFilePath = "__tmp__arctium.zip";
        DownloadFile(downloadUrl, tempFilePath);

        ZipFile.ExtractToDirectory(tempFilePath, arctiumPath);
        File.Delete(tempFilePath);
    }

    public static void PrepareHermesProxyData(string hermesPath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var allResources = assembly.GetManifestResourceNames();

        var baseCsvResources = allResources.Where(r => r.StartsWith("WinterspringLauncher.HermesDataPatches.") && !r.StartsWith("WinterspringLauncher.HermesDataPatches.Hotfix")).ToList();
        var hotfixCsvResources = allResources.Where(r => r.StartsWith("WinterspringLauncher.HermesDataPatches.Hotfix")).ToList();
        PatchCsvFiles(baseCsvResources, "WinterspringLauncher.HermesDataPatches.", Path.Combine(hermesPath, "CSV"));
        PatchCsvFiles(hotfixCsvResources, "WinterspringLauncher.HermesDataPatches.Hotfix.", Path.Combine(hermesPath, "CSV", "Hotfix"));

        var modifiedByUsFilePath = Path.Combine(hermesPath, "CSV", "_WinterspringLauncherModified.txt");
        if (!File.Exists(modifiedByUsFilePath))
            File.WriteAllText(modifiedByUsFilePath, "The CSV files where updated by WinterspringLauncher");

        void PatchCsvFiles(List<string> patchResources, string replace, string csvDirPath)
        {
            foreach (var patchResource in patchResources)
            {
                var fileName = patchResource.ReplaceFirstOccurrence(replace, "");
                var dstFilePath = Path.Combine(csvDirPath, fileName);

                List<string> patches = new List<string>();
                using (Stream stream = assembly.GetManifestResourceStream(patchResource)!)
                using (StreamReader reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        var currentLine = reader.ReadLine();
                        if (currentLine == null)
                            break;
                        patches.Add(currentLine);
                    }
                }

                if (!File.Exists(dstFilePath))
                {
                    Console.WriteLine($"Unable to patch '{dstFilePath}' ({patchResource})");
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
                
                var allLinesInCsv = File.ReadAllLines(dstFilePath).ToList();

                bool wasPatched = false;
                foreach (var patch in patches)
                {
                    if (patch.StartsWith("REMOVE!"))
                    {
                        var clearPatch = patch.Substring("REMOVE!".Length);
                        int amountRemoved = allLinesInCsv.RemoveAll((l) => l == clearPatch);
                        if (amountRemoved > 0)
                            wasPatched = true;
                    }
                    if (!allLinesInCsv.Contains(patch))
                    {
                        allLinesInCsv.Add(patch);
                        wasPatched = true;
                    }
                }

                if (wasPatched)
                {
                    Console.WriteLine($"Applying patch to '{dstFilePath}' ({patchResource})");
                    File.WriteAllLines(dstFilePath, allLinesInCsv);
                }
            }
        }
    }

    public static void PrepareHermesProxyConfig(string hermesPath, string realmlist)
    {
        var hermesConfigPath = Path.Combine(hermesPath, "HermesProxy.config");
        XmlDocument doc = new XmlDocument();
        doc.Load(hermesConfigPath);

        const string modifiedAttrName = "WinterspringLauncherModified";
        var configNode = doc.DocumentElement!.SelectSingleNode("/configuration")!;
        XmlNode? alreadyModifiedByUsMarker = configNode.SelectSingleNode(modifiedAttrName);
        if (alreadyModifiedByUsMarker == null)
        {
            XmlNode appSettings = configNode.SelectSingleNode("appSettings")!;

            XmlNode serverAddrNode = appSettings.SelectSingleNode("add[@key='ServerAddress']")!;
            serverAddrNode.Attributes!["value"]!.Value = realmlist;

            XmlNode packetLogNode = appSettings.SelectSingleNode("add[@key='PacketsLog']")!;
            packetLogNode.Attributes!["value"]!.Value = "false";

            var modifiedMarker = doc.CreateElement(modifiedAttrName);
            modifiedMarker.SetAttribute("comment", "This file might get overwritten when new Hermes update is applied");
            configNode.InsertBefore(modifiedMarker, configNode.FirstChild);

            doc.Save(hermesConfigPath);
        }
    }
}
