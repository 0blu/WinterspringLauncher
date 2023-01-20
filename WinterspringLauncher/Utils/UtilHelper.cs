namespace WinterspringLauncher.Utils;

public static class UtilHelper
{
    // Converts some arbitrary byte number to binary human unit (1024 -> "1.00 KiB")
    public static string ToHumanFileSize(long sizeInByte)
    {
        string[] units = { "Byte", "KiB", "MiB", "GiB", "TiB" };
        int unitIdx = 0;

        double size = sizeInByte;
        while (size >= 1024 && unitIdx < units.Length - 1) {
            unitIdx++;
            size /= 1024;
        }

        string unit = units[unitIdx];
        return $"{size:0.00} {unit}";
    }

    public static string ReplaceFirstOccurrence(this string source, string needle, string replacement, StringComparison comparison = StringComparison.InvariantCulture)
    {
        int pos = source.IndexOf(needle, comparison);
        if (pos == -1)
            return source;

        string result = source.Remove(pos, needle.Length).Insert(pos, replacement);
        return result;
    }


    public delegate bool CopyFolderFilterDelegate(string srcFileName);
    
    public static void CopyFolderRecursively(string srcFolder, string dstFolder, CopyFolderFilterDelegate? filter = null)
    {
        foreach (var srcFilePath in Directory.GetFiles(srcFolder, "*", SearchOption.AllDirectories))
        {
            var relPath = Path.GetRelativePath(srcFolder, srcFilePath);
            var dstFilePath = Path.Combine(dstFolder, relPath);
            if (filter?.Invoke(srcFilePath) ?? true)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dstFilePath)!);
                File.Copy(srcFilePath, dstFilePath, overwrite: true);
            }
        }
    }
}
