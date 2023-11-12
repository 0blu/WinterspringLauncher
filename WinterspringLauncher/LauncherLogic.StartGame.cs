using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Media;
using WinterspringLauncher.Utils;

namespace WinterspringLauncher;

public partial class LauncherLogic
{
    public void StartGame()
    {
        _model.InputIsAllowed = false;
        if (!_model.HermesIsRunning)
        {
            _model.AddLogEntry($"Launching...");
        }

        bool weAreOnMacOs = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        var serverInfo = _config.KnownServers.ElementAtOrDefault(_model.SelectedServerIdx);
        if (serverInfo == null)
        {
            _model.AddLogEntry("Error invalid server settings");
            _model.InputIsAllowed = true;
            return;
        }

        var gameInstallation = _config.GameInstallations.GetValueOrDefault(serverInfo.UsedInstallation);
        if (gameInstallation == null)
        {
            _model.AddLogEntry($"Error cant find '{serverInfo.UsedInstallation}' installation in settings");
            _model.InputIsAllowed = true;
            return;
        }

        if (!_model.HermesIsRunning)
        {
            _model.AddLogEntry($"---------- Selected Server Config ----------");
            _model.AddLogEntry($"Name: {serverInfo.Name}");
            _model.AddLogEntry($"Realmlist: {serverInfo.RealmlistAddress}");
            _model.AddLogEntry($"Game Directory: {gameInstallation.Directory}");
            _model.AddLogEntry($"Game Version: {gameInstallation.Version}");
            _model.AddLogEntry($"--------------------------------------------");
        }

        IBrush overallProgressColor = Brush.Parse("#4caf50");
        IBrush sideProgressColor = Brush.Parse("#553399");
        Task.Run(async () =>
        {
            if (_model.HermesIsRunning)
            {
                _model.AddLogEntry("Starting another game instance");
                _model.SetProgressbar("Starting Game", 90, overallProgressColor);
                LauncherActions.PrepareGameConfigWtf(gameInstallation.Directory, portalAddress: "127.0.0.1:1119");

                _model.SetProgressbar("Starting Game", 95, overallProgressColor);
                await Task.Delay(TimeSpan.FromSeconds(0.5));
                LauncherActions.StartGame(Path.Combine(gameInstallation.Directory, SubPathToWowForCustomServers));
                await Task.Delay(TimeSpan.FromSeconds(5));
                return;
            }

            _model.SetProgressbar("Checking WoW installation", 10, overallProgressColor);
            _model.AddLogEntry("Checking WoW installation");
            await Task.Delay(TimeSpan.FromSeconds(0.5));

            bool clientWasDownloadedInThisSession = false; // required to get at least the .build.info once, even if disabled
            var expectedPatchedClientLocation = Path.Combine(gameInstallation.Directory, SubPathToWowForCustomServers);
            if (!File.Exists(expectedPatchedClientLocation))
            {
                _model.AddLogEntry($"Patched client was NOT found at \"{expectedPatchedClientLocation}\"");

                // Checking default WoW installation
                var expectedDefaultClientLocation = Path.Combine(gameInstallation.Directory, SubPathToWowOriginal);
                if (!File.Exists(expectedDefaultClientLocation))
                {
                    _model.AddLogEntry($"Default wow client was NOT found at \"{expectedDefaultClientLocation}\"");

                    _model.AddLogEntry("Downloading WoW Client...");

                    if (!gameInstallation.BaseClientDownloadURL.TryGetValue(weAreOnMacOs ? OperatingSystem.MacOs : OperatingSystem.Windows, out string? downloadUrl))
                    {
                        _model.AddLogEntry($"Cant find download url for \"{(weAreOnMacOs ? OperatingSystem.MacOs : OperatingSystem.Windows)}\"");
                        return;
                    }

                    _model.AddLogEntry($"Download URL: {downloadUrl}");
                    var targetDir = new DirectoryInfo(FullPath(gameInstallation.Directory)).FullName;
                    if (!Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    var downloadDestLocation = targetDir + ".partial-download";
                    _model.AddLogEntry($"Download Location: {downloadDestLocation}");

                    var exisingFile = new FileInfo(downloadDestLocation);
                    if (exisingFile.Exists && exisingFile.Length > 2_000_000_000) // >2GB
                    {
                        await Task.Delay(TimeSpan.FromSeconds(0.5));
                        _model.AddLogEntry("Detected downloaded file. Is it already downloaded?");
                        await Task.Delay(TimeSpan.FromSeconds(5));
                        _model.AddLogEntry("Skipping download");
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                    else
                    {
                        _model.SetProgressbar("Downloading WoW", 0, Brush.Parse("#1976d2"));
                        try
                        {
                            RunDownload(downloadUrl, downloadDestLocation);
                        }
                        catch when (false)
                        {
                            // TODO: Ask user for manual selecting a zip/rar file
                        }
                    }

                    _model.AddLogEntry($"Unpack to: {targetDir}");
                    _model.SetProgressbar("Unpack WoW", 0, Brush.Parse("#d84315"));
                    RunUnpack(downloadDestLocation, targetDir);
                    
                    #if !DEBUG
                    try
                    {
                        File.Delete(downloadDestLocation);
                    }
                    catch(Exception e)
                    {
                        _model.AddLogEntry($"Failed to delete tmp file '{downloadDestLocation}'");
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                    #endif
                }

                try {
                    if (!File.Exists(expectedPatchedClientLocation) || (_config.CheckForClientPatchUpdates))
                    {
                        _model.SetProgressbar("Checking WoW patch status", 30, overallProgressColor);
                        await Task.Delay(TimeSpan.FromSeconds(0.5));

                        string summaryUrl = gameInstallation.ClientPatchInfoURL;

                        _model.AddLogEntry($"Summary URL: {summaryUrl}");
                        var patchSummary = SimpleFileDownloader.PerformGetJsonRequest<BinaryPatchHandler.PatchSummary>(summaryUrl);

                        var selectedPatchInfo = weAreOnMacOs ? patchSummary.MacOs : patchSummary.Windows;
                        if (selectedPatchInfo == null)
                            throw new Exception($"No path for '{(weAreOnMacOs ? "macos" : "windows")}' was found");

                        if (!File.Exists(expectedPatchedClientLocation) || selectedPatchInfo.ToSha256 != HashHelper.CreateHexSha256HashFromFilename(expectedDefaultClientLocation))
                        {
                            _model.AddLogEntry("Patched client update required");
                            var patchUrl = string.Join("/", summaryUrl.Split("/").SkipLast(1)) + $"/{selectedPatchInfo.PatchFilename}";
                            _model.AddLogEntry($"Patch URL: {patchUrl}");
                            await Task.Delay(TimeSpan.FromSeconds(0.5));

                            var patchFileContent = SimpleFileDownloader.PerformGetBytesRequest(patchUrl);
                            BinaryPatchHandler.ApplyPatch(patchFileContent, sourceFile: expectedDefaultClientLocation, targetFile: expectedPatchedClientLocation);
                            _model.AddLogEntry("Patch was applied!");
                            await Task.Delay(TimeSpan.FromSeconds(0.5));
                        }
                    }

                    clientWasDownloadedInThisSession = true;
                }
                catch (Exception e) when (File.Exists(expectedPatchedClientLocation))
                {
                    _model.AddLogEntry("Failed to check for an update for the client");
                    _model.AddLogEntry("But since the file exists this error can be ignored");
                    await Task.Delay(TimeSpan.FromSeconds(0.5));
                    _model.AddLogEntry(e.ToString());
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    Console.WriteLine(e);
                }

                _model.GameIsInstalled = true;
            }

            bool buildInfoWasChanged = false;
            if (gameInstallation.CustomBuildInfoURL != null && (clientWasDownloadedInThisSession || _config.CheckForClientBuildInfoUpdates))
            {
                _model.SetProgressbar("Checking BuildInfo status", 35, overallProgressColor);

                string buildInfoFilePath = Path.Combine(gameInstallation.Directory, ".build.info");

                string newBuildInfo;
                try
                {
                    newBuildInfo = SimpleFileDownloader.PerformGetStringRequest(gameInstallation.CustomBuildInfoURL);
                }
                catch
                {
                    _model.AddLogEntry($"BuildInfo URL: {gameInstallation.CustomBuildInfoURL}");
                    throw;
                }

                string existingBuildInfo = File.Exists(buildInfoFilePath) ? File.ReadAllText(buildInfoFilePath) : string.Empty;

                if (newBuildInfo.ReplaceLineEndings() != existingBuildInfo.ReplaceLineEndings())
                {
                    _model.AddLogEntry("BuildInfo update detected");
                    await Task.Delay(TimeSpan.FromSeconds(0.5));
                    File.WriteAllText(buildInfoFilePath, newBuildInfo);
                    buildInfoWasChanged = true;
                }
            }

            _model.AddLogEntry("Checking HermesProxy status");
            _model.SetProgressbar("Checking HermesProxy status", 50, overallProgressColor);
            await Task.Delay(TimeSpan.FromSeconds(0.5));

            string? localHermesVersion = null;
            var hermesProxyVersionFile = Path.Combine(_config.HermesProxyLocation, "version.txt");
            if (File.Exists(hermesProxyVersionFile))
            {
                localHermesVersion = File.ReadLines(hermesProxyVersionFile).First();
            }

            if (localHermesVersion == null || _config.CheckForHermesUpdates)
            {
                GitHubReleaseInfo releaseInfo = GitHubApi.LatestReleaseVersion("WowLegacyCore/HermesProxy");
                var versionString = $"{releaseInfo.TagName}|{releaseInfo.Name}";
                if (localHermesVersion != versionString)
                {
                    var osName = weAreOnMacOs ? "mac" : "win";
                    var possibleDownloads = releaseInfo.Assets!.FindAll(a => a.Name.Contains(osName, StringComparison.CurrentCultureIgnoreCase));
                    if (possibleDownloads.Count != 1)
                        throw new Exception($"Found {possibleDownloads.Count} HermesProxy versions for your OS");

                    var targetDir = new DirectoryInfo(FullPath(_config.HermesProxyLocation)).FullName;
                    if (!Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);

                    var downloadDestLocation = targetDir + ".partial-download";

                    _model.SetProgressbar("Downloading HermesProxy", 0, Brush.Parse("#1976d2"));
                    var downloadUrl = possibleDownloads[0].DownloadUrl;
                    _model.AddLogEntry($"Download URL: {downloadUrl}");
                    _model.AddLogEntry($"Download Location: {downloadDestLocation}");
                    RunDownload(downloadUrl, downloadDestLocation);

                    var directories = Directory.GetDirectories(targetDir);
                    foreach (string directory in directories)
                    {
                        if (!directory.Contains("AccountData")) // we want to keep our AccountData
                            Directory.Delete(directory, recursive: true);
                    }

                    Directory.CreateDirectory(targetDir);

                    _model.SetProgressbar("Unpack HermesProxy", 0, Brush.Parse("#d84315"));
                    RunUnpack(downloadDestLocation, targetDir);

                    File.WriteAllLines(hermesProxyVersionFile, new string[]
                    {
                        versionString,
                        $"Source: {downloadUrl}"
                    });

                    _model.SetHermesVersion(releaseInfo.TagName);
                }
            }

            var modernBuild = ushort.Parse(gameInstallation.Version.Split(".").Last());

            _model.AddLogEntry($"-----------------");
            _model.SetProgressbar("Starting HermesProxy", 75, overallProgressColor);
            await Task.Delay(TimeSpan.FromSeconds(0.5));

            _model.AddLogEntry($"ModernBuild: {modernBuild}");

            var hermesSettingsOverwrite = new Dictionary<string, string>();

            var splittedRealmlist = serverInfo.RealmlistAddress.Split(':');
            hermesSettingsOverwrite.Add("ServerAddress", splittedRealmlist.First());
            if (splittedRealmlist.Length == 2)
                hermesSettingsOverwrite.Add("ServerPort", splittedRealmlist.Last());

            var hermesProcess = LauncherActions.StartHermesProxy(_config.HermesProxyLocation, modernBuild, hermesSettingsOverwrite, (logLine) => { _model.AddLogEntry(logLine); });
            _model.SetHermesPid(hermesProcess.Id);
            hermesProcess.Exited += (a, e) =>
            {
                _model.AddLogEntry($"HERMES PROXY HAS CLOSED! Status: {hermesProcess.ExitCode}");
                _model.SetHermesPid(null);
            };
            await Task.Delay(TimeSpan.FromSeconds(1));
            if (hermesProcess.HasExited)
            {
                _model.AddLogEntry($"HERMES PROXY HAS CLOSED PREMATURELY! Status: {hermesProcess.ExitCode}");
                _model.SetHermesPid(null);
            }

            _model.SetProgressbar("Starting Game", 90, overallProgressColor);
            LauncherActions.PrepareGameConfigWtf(gameInstallation.Directory, portalAddress: "127.0.0.1:1119");

            if (buildInfoWasChanged)
                _model.SetProgressbar("Your game is updating please wait a bit (check Task Manager!)", 95, sideProgressColor);
            else
                _model.SetProgressbar("Starting Game", 95, overallProgressColor);

            LauncherActions.StartGame(Path.Combine(gameInstallation.Directory, SubPathToWowForCustomServers));
            await Task.Delay(TimeSpan.FromSeconds(5));
        }).ContinueWith((t) =>
        {
            if (t.Exception != null)
            {
                _model.AddLogEntry(t.Exception.ToString());
            }
            else if (t.IsCompletedSuccessfully)
            {
                _model.SetProgressbar("Done", 100, overallProgressColor);
                _model.InputIsAllowed = true;
            }
        });
    }
}
