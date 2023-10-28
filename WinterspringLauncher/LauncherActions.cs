using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using WinterspringLauncher.Utils;

namespace WinterspringLauncher;

public static class LauncherActions
{
    public delegate void DownloadProgressInfoHandler(long? totalBytes, long alreadyDownloadedBytes, long bytesPerSec);
    public delegate void UnpackProgressInfoHandler(long totalFileCount, long alreadyUnpackedFileCount);

    public static void DownloadFile(string downloadUrl, string downloadDestLocation, DownloadProgressInfoHandler progressInfoHandler)
    {
        using (var client = new ProgressiveFileDownloader(downloadUrl, downloadDestLocation))
        {
            client.ProgressChangedFixedDelay += (totalBytes, alreadyDownloadedBytes, bytePerSec) =>
            {
                progressInfoHandler(totalBytes, alreadyDownloadedBytes, bytePerSec);
            };

            client.DownloadDone += (downloadedBytes) => {
                progressInfoHandler(downloadedBytes, downloadedBytes, 0);
            };

            client.StartGetDownload().Wait();
        }
    }

    public static void PrepareGameConfigWtf(string gamePath, string portalAddress)
    {
        var configWtfPath = Path.Combine(gamePath, "_classic_era_", "WTF", "Config.wtf");
        var dirName = Path.GetDirectoryName(configWtfPath);
        Directory.CreateDirectory(dirName!);

        List<string> configContent;
        if (!File.Exists(configWtfPath))
        {
            // TODO Take the language from this launcher
            configContent = new List<string>();
            string bestDefaultTextLocale = CultureInfo.CurrentCulture.Name.StartsWith("zh", StringComparison.InvariantCultureIgnoreCase)
                    ? "zhCN"
                    : "enUS";
            configContent.Add($"SET textLocale {bestDefaultTextLocale}");
        }
        else
        {
            configContent = File.ReadAllLines(configWtfPath).ToList();
        }

        var newLine = $"SET portal \"{portalAddress}\"";
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

    public static void Unpack(string compressedArchivePath, string targetDir, UnpackProgressInfoHandler progressInfoHandler)
    {
        ArchiveCompression.Decompress(compressedArchivePath, targetDir, "World of Warcraft",
            (totalFileCount, alreadyUnpackedFileCount) =>
            {
                progressInfoHandler(totalFileCount, alreadyUnpackedFileCount);
            });
    }

    public delegate void OnLogLine(string logLine);
    
    public static Process StartHermesProxy(string hermesDir, ushort modernClientBuild, Dictionary<string, string> settingsOverwrite, OnLogLine logLine)
    {
        bool weAreOnMacOs = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        var executableName = weAreOnMacOs
            ? "HermesProxy"
            : "HermesProxy.exe";

        var executablePath = Path.Combine(hermesDir, executableName);

        var procInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = hermesDir,
            RedirectStandardOutput = true,
            ArgumentList = {
                "--no-version-check",
                "--set", $"ClientBuild={modernClientBuild}",
            },
        };

        foreach (var (key, value) in settingsOverwrite)
        {
            procInfo.ArgumentList.Add("--set");
            procInfo.ArgumentList.Add($"{key}={value}");
        }

        Console.WriteLine("Starting HermesProxy with arguments: ");
        for (var i = 0; i < procInfo.ArgumentList.Count; i++)
            Console.WriteLine($"[{i}] {procInfo.ArgumentList[i]}");
        var process = Process.Start(procInfo)!;

        process.EnableRaisingEvents = true;
        process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
        {
            if (!String.IsNullOrEmpty(e.Data))
            {
                logLine(e.Data);
            }
        });
        process.BeginOutputReadLine();

        return process;
    }

    public static void StartGame(string executablePath)
    {
        bool weAreOnMacOs = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        ProcessStartInfo startInfo;
        if (weAreOnMacOs)
        {
            startInfo = new ProcessStartInfo
            {
                // We are here "_classic_era_/WoW For Custom Servers.app/Contents/MacOS/WoW For Custom Servers"
                // and want to be here "_classic_era_"

                FileName = "/usr/bin/open",
                ArgumentList = { "--new", "--wait-apps", $"./{Path.GetDirectoryName(Path.Combine(executablePath, "..", ".."))}" },
                WorkingDirectory = Path.GetDirectoryName(Path.Combine(executablePath, "..", "..", "..")),
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
                FileName = Path.GetFileName(executablePath),
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                UseShellExecute = true,
                CreateNoWindow = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                RedirectStandardOutput = false,
            };
        }

        //startInfo.EnvironmentVariables.Clear();
        var process = Process.Start(startInfo)!;

    }
}
