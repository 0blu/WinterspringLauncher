using System;

namespace WinterspringLauncher.Utils;

public static class UtilHelper
{
    // Converts some arbitrary byte number to binary human unit (1024 -> "1.0 KiB")
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
        return $"{size:0.0} {unit}";
    }

    public static string ReplaceFirstOccurrence(this string source, string needle, string replacement, StringComparison comparison = StringComparison.InvariantCulture)
    {
        int pos = source.IndexOf(needle, comparison);
        if (pos == -1)
            return source;

        string result = source.Remove(pos, needle.Length).Insert(pos, replacement);
        return result;
    }
}
