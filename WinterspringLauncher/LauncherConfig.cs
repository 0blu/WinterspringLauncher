using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using WinterspringLauncher.Utils;

namespace WinterspringLauncher;

public enum OperatingSystem
{
    Windows,
    MacOs
}

public class VersionedBaseConfig
{
    public int ConfigVersion { get; set; } = 2;
}

public class LauncherConfig : VersionedBaseConfig
{
    public string LauncherLanguage { get; set; } = "en";
    public string? GitHubMirror { get; set; } = null; // example "https://asia.cdn.everlook-wow.net/github-mirror/" + "/repos/{repoName}/releases/latest"
    public string LastSelectedServerName { get; set; } = "";
    public bool CheckForLauncherUpdates { get; set; } = true;
    public bool CheckForHermesUpdates { get; set; } = true;
    public bool CheckForClientPatchUpdates { get; set; } = true;
    public bool CheckForClientBuildInfoUpdates { get; set; } = true;

    public ServerInfo[] KnownServers { get; set; } = new ServerInfo[]
    {
        new ServerInfo
        {
            Name = "Everlook (Europe)",
            RealmlistAddress = "logon.everlook.org",
            UsedInstallation = "Everlook EU 1.14.2 installation"
        },
        new ServerInfo
        {
            Name = "Everlook (Asia)",
            RealmlistAddress = "asia.everlook-wow.net",
            UsedInstallation = "Everlook Asia 1.14.2 installation",
        },
        new ServerInfo
        {
            Name = "Localhost (1.14.2)",
            RealmlistAddress = "127.0.0.1",
            UsedInstallation = "Default 1.14.2 installation",
            HermesSettings = new Dictionary<string, string>
            {
                ["DebugOutput"] = "true",
                ["PacketsLog"] = "true",
            }
        },
    };

    public Dictionary<string, InstallationLocation> GameInstallations { get; set; } = new Dictionary<string, InstallationLocation>
    {
        ["Everlook EU 1.14.2 installation"] = new InstallationLocation
        {
            Directory = "./winterspring-data/WoW 1.14.2 Everlook",
            Version = "1.14.2.42597",
            ClientPatchInfoURL = "https://wow-patches.blu.wtf/patches/1.14.2.42597_summary.json", 
            CustomBuildInfoURL = "https://eu.cdn.everlook.org/everlook_europe_1.14.2_prod/.build.info",
            BaseClientDownloadURL = new Dictionary<OperatingSystem, string>() {
                [OperatingSystem.Windows] = "https://download.wowdl.net/downloadFiles/Clients/WoW%20Classic%201.14.2.42597%20All%20Languages.rar",
                [OperatingSystem.MacOs] = "https://download.wowdl.net/downloadFiles/Clients/WoW_Classic_1.14.2.42597_macOS.zip",
            },
        },
        ["Everlook Asia 1.14.2 installation"] = new InstallationLocation
        {
            Directory = "./winterspring-data/WoW 1.14.2 Everlook Asia",
            Version = "1.14.2.42597",
            ClientPatchInfoURL = "https://wow-patches.blu.wtf/patches/1.14.2.42597_summary.json", 
            CustomBuildInfoURL = "https://asia.cdn.everlook.org/everlook_asia_1.14.2_prod/.build.info",
            BaseClientDownloadURL = new Dictionary<OperatingSystem, string>() {
                [OperatingSystem.Windows] = "http://asia.cdn.everlook.aclon.cn/game-client-patch-cdn/wow_classic_1_14_2_42597_all_languages.rar",
                [OperatingSystem.MacOs] = "http://asia.cdn.everlook.aclon.cn/game-client-patch-cdn/wow_classic_1_14_2_42597_all_languages_macos.rar",
            },
        },
        ["Default 1.14.2 installation"] = new InstallationLocation
        {
            Directory = "./winterspring-data/WoW 1.14.2",
            Version = "1.14.2.42597",
            ClientPatchInfoURL = "https://wow-patches.blu.wtf/patches/1.14.2.42597_summary.json",
            BaseClientDownloadURL = new Dictionary<OperatingSystem, string>() {
                [OperatingSystem.Windows] = "https://download.wowdl.net/downloadFiles/Clients/WoW%20Classic%201.14.2.42597%20All%20Languages.rar",
                [OperatingSystem.MacOs] = "https://download.wowdl.net/downloadFiles/Clients/WoW_Classic_1.14.2.42597_macOS.zip",
            },
        }
    };

    public string HermesProxyLocation { get; set; } = "./winterspring-data/HermesProxy";

    public class ServerInfo
    {
        public string Name { get; set; }
        public string RealmlistAddress { get; set; }
        public string UsedInstallation { get; set; }
        //public bool? RequiresHermes { get; set; }
        public Dictionary<string, string>? HermesSettings { get; set; }
    }

    public class InstallationLocation
    {
        public string Version { get; set; }
        public string Directory { get; set; }
        public string ClientPatchInfoURL { get; set; }
        public string? CustomBuildInfoURL { get; set; } // Optional
        public Dictionary<OperatingSystem, string> BaseClientDownloadURL { get; set; }
    }

    public static LauncherConfig GetDefaultConfig() => new LauncherConfig();

    public void SaveConfig(string configPath)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(this, options);
        File.WriteAllText(configPath, jsonString, Encoding.UTF8);
    }

    public static LauncherConfig LoadOrCreateDefault(string configPath)
    {
        LauncherConfig config;
        if (!File.Exists(configPath))
        {
            config = GetDefaultConfig();
        }
        else
        {
            string configTextContent = File.ReadAllText(configPath, Encoding.UTF8);
            string updatedConfig = PatchConfigIfNeeded(configTextContent);
            var loadedJson = JsonSerializer.Deserialize<LauncherConfig>(updatedConfig);
            if (loadedJson != null)
            {
                config = loadedJson;
            }
            else
            {
                Console.WriteLine("Config is null after loading? Replacing it with default one");
                config = GetDefaultConfig();
            }
        }

        config.SaveConfig(configPath);

        return config;
    }

    private static string PatchConfigIfNeeded(string currentConfig)
    {
        var configVersion = JsonSerializer.Deserialize<VersionedBaseConfig>(currentConfig);
        if (configVersion == null)
        {
            Console.WriteLine("Unable to determine config version");
            return currentConfig;
        }

        if (configVersion.ConfigVersion >= 2)
            return currentConfig; // already on latest version

        if (configVersion.ConfigVersion == 1)
        {
            var v1Config = JsonSerializer.Deserialize<LegacyV1Config>(currentConfig);
            if (v1Config == null)
                return currentConfig; // Error ?

            var v2Config = new LauncherConfig();

            // If a official everlook server is detected switch the installation directory, so the client does not need to redownload it
            if (v1Config.Realmlist.Contains("everlook-wow.net", StringComparison.InvariantCultureIgnoreCase))
            {
                var knownServer = v2Config.KnownServers.First(g => g.RealmlistAddress.Contains("everlook-wow", StringComparison.InvariantCultureIgnoreCase));
                var knownInstallation = v2Config.GameInstallations.First(g => g.Key == knownServer.UsedInstallation);
                v2Config.GitHubMirror = "https://asia.cdn.everlook-wow.net/github-mirror/api/";
                v2Config.LastSelectedServerName = knownServer.Name;
                TryUpgradeOldGameFolder(knownInstallation.Value.Directory, v1Config.GamePath);
            }
            else if (v1Config.Realmlist.Contains("everlook.org", StringComparison.InvariantCultureIgnoreCase))
            {
                var knownServer = v2Config.KnownServers.First(g => g.RealmlistAddress.Contains("everlook.org", StringComparison.InvariantCultureIgnoreCase));
                var knownInstallation = v2Config.GameInstallations.First(g => g.Key == knownServer.UsedInstallation);
                v2Config.LastSelectedServerName = knownServer.Name;
                TryUpgradeOldGameFolder(oldGameFolder: v1Config.GamePath, newGameFolder: knownInstallation.Value.Directory);
            }

            return JsonSerializer.Serialize(v2Config);
        }

        Console.WriteLine("Unknown version");
        return currentConfig;
    }

    private static void TryUpgradeOldGameFolder(string oldGameFolder, string newGameFolder)
    {
        try
        {
            bool weAreOnMacOs = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            if (!weAreOnMacOs)
            {
                string known_1_14_2_client_hash = "43F407C7915602D195812620D68C3E5AE10F20740549D2D63A0B04658C02A123";

                var gameExecutablePath = Path.Combine(oldGameFolder, "_classic_era_", "WoWClassic.exe");

                if (File.Exists(gameExecutablePath) && HashHelper.CreateHexSha256HashFromFilename(gameExecutablePath) == known_1_14_2_client_hash)
                {
                    // We can just move the whole folder
                    Directory.Move(oldGameFolder, newGameFolder); // <-- might fail if target is not empty
                }
                else
                {
                    // Just copy the WTF and Interface folder

                    var oldInterfaceFolder = Path.Combine(oldGameFolder, "_classic_era_", "Interface");
                    var newInterfaceFolder = Path.Combine(newGameFolder, "_classic_era_", "Interface");
                    DirectoryCopy.Copy(oldInterfaceFolder, newInterfaceFolder);

                    var oldWtfFolder = Path.Combine(oldGameFolder, "_classic_era_", "WTF");
                    var newWtfFolder = Path.Combine(newGameFolder, "_classic_era_", "WTF");
                    DirectoryCopy.Copy(oldWtfFolder, newWtfFolder);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Error while TryUpgradeOldGameFolder");
            Console.WriteLine(e);
        }
    }

    private class LegacyV1Config : VersionedBaseConfig
    {
        public string GitRepoWinterspringLauncher { get; set; }
        public string GitRepoHermesProxy { get; set; }
        public string GitRepoArctiumLauncher { get; set; }

        public string WindowsGameDownloadUrl { get; set; }
        public string MacGameDownloadUrl { get; set; }
        public string GamePatcherUrl { get; set; }

        public string HermesProxyPath { get; set; }
        public string GamePath { get; set; }
        public string ArctiumLauncherPath { get; set; }
        public bool RecreateDesktopShortcut { get; set; }
        public bool AutoUpdateThisLauncher { get; set; }

        public string Realmlist { get; set; }
    }
}

