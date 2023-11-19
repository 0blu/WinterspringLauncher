using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using WinterspringLauncher.Utils;
using WinterspringLauncher.ViewModels;
using WinterspringLauncher.Views;

namespace WinterspringLauncher;

public partial class LauncherLogic
{
    private const string CONFIG_FILE_NAME = "winterspring-launcher-config.json";

    private readonly MainWindowViewModel _model;
    private readonly LauncherConfig _config;

    private string FullPath(string subPath) => Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, subPath));

    private static readonly string SubPathToWowOriginal = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? "_classic_era_/World of Warcraft Classic.app/Contents/MacOS/World of Warcraft Classic"
        : "_classic_era_/WowClassic.exe";

    private static readonly string SubPathToWowForCustomServers = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? "_classic_era_/WoW For Custom Servers.app/Contents/MacOS/WoW For Custom Servers"
        : "_classic_era_/WowClassic_ForCustomServers.exe";

    public LauncherLogic(MainWindowViewModel model)
    {
        _model = model;

        _config = LauncherConfig.LoadOrCreateDefault(CONFIG_FILE_NAME);

        if (_config.LastSelectedServerName == "") // first configuration
        {
            _config.LastSelectedServerName = LocaleDefaults.GetBestServerName();
            _config.GitHubApiMirror = LocaleDefaults.GetBestGitHubMirror();
        }

        if (!string.IsNullOrWhiteSpace(_config.GitHubApiMirror))
            GitHubApi.GitHubApiAddress = _config.GitHubApiMirror;

        for (var i = 0; i < _config.KnownServers.Length; i++)
        {
            var knownServer = _config.KnownServers[i];
            _model.KnownServerList.Add(knownServer.Name);
            if (_config.LastSelectedServerName == knownServer.Name)
                _model.SelectedServerIdx = i;
        }

        _model.Language.SetLanguage(_config.LauncherLanguage);

        _model.AddLogEntry($"Launcher started");
        _model.AddLogEntry($"Base path: \"{FullPath(".")}\"");
        _model.AddLogEntry($"GitHub API Address: {GitHubApi.GitHubApiAddress}");

        string? localHermesVersion = null;
        var hermesProxyVersionFile = Path.Combine(_config.HermesProxyLocation, "version.txt");
        if (File.Exists(hermesProxyVersionFile))
        {
            localHermesVersion = File.ReadLines(hermesProxyVersionFile).First().Split("|")[0];
        }
        _model.SetHermesVersion(localHermesVersion);


        if (_config.CheckForLauncherUpdates)
        {
            Task.Run(() =>
            {
                try
                {
                    if (LauncherVersion.CheckIfUpdateIsAvailable(out var updateInformation))
                    {
                        _model.AddLogEntry($"--------------------------");
                        _model.AddLogEntry($"This launcher has a new version {updateInformation.VersionName} ({updateInformation.ReleaseDate:yyyy-MM-dd})");
                        _model.AddLogEntry($"You can download it here {updateInformation.URLLinkToReleasePage}");
                        _model.AddLogEntry($"--------------------------");
                        CreateUpdatePopup(updateInformation);
                    }
                    Console.WriteLine("Launcher update check done");
                }
                catch (Exception e)
                {
                    _model.AddLogEntry("An error occured while checking for a launcher update");
                    Console.WriteLine(e);
                }
            });
        }
    }

    private void CreateUpdatePopup(LauncherVersion.UpdateInformation updateInformation)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var dialog = new NewVersionAvailableDialog(updateInformation);
                dialog.ShowDialog(desktop.MainWindow);
            }
        });
    }

    public void ChangeServerIdx()
    {
        var serverInfo = _config.KnownServers.ElementAtOrDefault(_model.SelectedServerIdx);
        if (serverInfo == null)
        {
            _model.AddLogEntry("Error invalid server settings");
            _model.InputIsAllowed = true;
            return;
        }

        Console.WriteLine($"Selected Server: {serverInfo.Name}");
        var gameInstallation = _config.GameInstallations.GetValueOrDefault(serverInfo.UsedInstallation);
        if (gameInstallation == null)
        {
            _model.AddLogEntry($"Error cant find '{serverInfo.UsedInstallation}' installation in settings");
            _model.InputIsAllowed = true;
            return;
        }

        _config.LastSelectedServerName = serverInfo.Name;
        _config.SaveConfig(CONFIG_FILE_NAME);

        var expectedPatchedClientLocation = Path.Combine(gameInstallation.Directory, SubPathToWowForCustomServers);
        _model.GameFolderExists = Directory.Exists(gameInstallation.Directory);
        _model.GameIsInstalled = File.Exists(expectedPatchedClientLocation);

        _model.GameVersion = string.Join('.', gameInstallation.Version.Split('.').SkipLast(1));
    }

    private void RunDownload(string downloadUrl, string destLocation)
    {
        LauncherActions.DownloadFile(downloadUrl, destLocation,
            (totalBytes, alreadyDownloadedBytes, bytesPerSec) =>
            {
                double percent = (totalBytes != null)
                    ? (alreadyDownloadedBytes / (double)totalBytes.Value) * 100
                    : 0;

                string additionalText = $"   {UtilHelper.ToHumanFileSize(alreadyDownloadedBytes)}/{UtilHelper.ToHumanFileSize(totalBytes ?? 0)}   {UtilHelper.ToHumanFileSize(bytesPerSec)}/s   ";
                _model.UpdateProgress(percent, additionalText);
            });
    }

    private void RunUnpack(string archiveLocation, string targetDir)
    {
        LauncherActions.Unpack(archiveLocation, targetDir,
            (totalFileCount, alreadyUnpacked) =>
            {
                double percent = (alreadyUnpacked / (double)totalFileCount) * 100;
                _model.UpdateProgress(percent, $"   {alreadyUnpacked} / {totalFileCount}   ");
            });
    }

    public void KillHermesProxy()
    {
        try
        {
            _hermesProcess?.Kill();
        }
        catch(Exception e)
        {
            _model.AddLogEntry("Fail to stop HermesProxy");
            Console.WriteLine("Fail to kill HermesProxy");
            Console.WriteLine(e);
        }
    }
}
