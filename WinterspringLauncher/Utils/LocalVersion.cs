using System.Text;

namespace WinterspringLauncher.Utils;

public static class LocalVersion
{
    private const string VERSION_TXT = "version.txt";
    
    public static string? GetLocalVersion(string modulePath)
    {
        var localVersionFile = Path.Combine(modulePath, VERSION_TXT);
        if (!File.Exists(localVersionFile))
            return null;
        return File.ReadAllLines(localVersionFile)[0].Trim();
    }

    public static void WriteLocalVersion(string modulePath, string version, string source)
    {
        var localVersionFile = Path.Combine(modulePath, VERSION_TXT);
        File.WriteAllLines(localVersionFile, new[]
        {
            version,
            $"Source: {source}"
        }, Encoding.UTF8);
    }
}
