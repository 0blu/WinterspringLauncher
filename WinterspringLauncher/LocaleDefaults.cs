using System;
using System.Globalization;

namespace WinterspringLauncher;

public static class LocaleDefaults
{
    public static bool ShouldUseAsiaPreferences { get; set; } = CultureInfo.CurrentCulture.Name.StartsWith("zh", StringComparison.InvariantCultureIgnoreCase);

    public static string GetBestWoWConfigLocale()
    {
        return ShouldUseAsiaPreferences ? "zhCN" : "enUS";
    }

    public static string? GetBestGitHubMirror()
    {
        return ShouldUseAsiaPreferences ? "https://asia.cdn.everlook.aclon.cn/github-mirror/api/" : null;
    }

    public static string GetBestServerName()
    {
        return ShouldUseAsiaPreferences ? "Everlook (Asia)" : "Everlook (Europe)";
    }
}
