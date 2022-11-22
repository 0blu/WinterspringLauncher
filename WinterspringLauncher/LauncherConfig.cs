using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinterspringLauncher;

public class LauncherConfig
{
    public const string DEFAULT_DOWNLOAD_URL = "default";

    public int ConfigVersion { get; set; } = 1;

    [Obsolete("Replaced with 'GitRepoWinterspringLauncher'")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GitRepoEverlookClassic  { get; set; }
    public string GitRepoWinterspringLauncher { get; set; } = "0blu/WinterspringLauncher";
    public string GitRepoHermesProxy { get; set; } = "WowLegacyCore/HermesProxy";
    public string GitRepoArctiumLauncher { get; set; } = "Arctium/WoW-Launcher";

    public string WindowsGameDownloadUrl { get; set; } = DEFAULT_DOWNLOAD_URL;
    public string MacGamePatchDownloadUrl { get; set; } = DEFAULT_DOWNLOAD_URL;

    public string HermesProxyPath { get; set; } = "./hermes-proxy";
    public string GamePath { get; set; } = "./game-client";
    public string ArctiumLauncherPath { get; set; } = "./arctium-launcher";
    public bool RecreateDesktopShortcut { get; set; } = true;

    public string Realmlist { get; set; } = "logon.everlook.org";

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
            var configTextContent = File.ReadAllText(configPath, Encoding.UTF8);
            var loadedJson = JsonSerializer.Deserialize<LauncherConfig>(configTextContent);
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

        PatchConfigIfNeeded(config);
        config.SaveConfig(configPath);

        return config;
    }

    private static void PatchConfigIfNeeded(LauncherConfig config)
    {
#pragma warning disable CS0618
        if (config.GitRepoEverlookClassic != null)
            config.GitRepoEverlookClassic = null; // remove old repo from config
#pragma warning restore CS0618

        if (config.ConfigVersion < 2)
        {
            // For future use
        }
    }
}
