using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace WinterspringLauncher;

public partial class LauncherLogic
{
    public void OpenGameFolder()
    {
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

        var absPath = Path.GetFullPath(gameInstallation.Directory);
        if (!Directory.Exists(absPath))
        {
            _model.AddLogEntry("Game folder does not exists");
            return;
        }
        
        _model.AddLogEntry("Opening game folder");
        try
        {
            if (weAreOnMacOs)
                Process.Start("open", $"-R \"{absPath}\"");
            else
                Process.Start("explorer.exe", absPath);
        }
        catch (Exception e)
        {
            _model.AddLogEntry($"An error occured while opening game folder");
            Console.WriteLine(e);
        }
    }
}
