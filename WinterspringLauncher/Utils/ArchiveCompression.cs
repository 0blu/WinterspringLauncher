using System.IO.Compression;
using System.Reflection;

#if PLATFORM_WINDOWS
using SevenZip;
#endif

namespace WinterspringLauncher.Utils;

public static class ArchiveCompression
{
    public static void DecompressWithProgress(string archiveFilePath, string extractionFolderPath)
    {
        if (Path.GetExtension(archiveFilePath).Equals(".zip", StringComparison.InvariantCultureIgnoreCase))
        {
            DecompressZipWithProgress(archiveFilePath, extractionFolderPath);
        }
        else
        {
            Decompress7ZWithProgress(archiveFilePath, extractionFolderPath);
        }
    }

    private static void DecompressZipWithProgress(string archiveFilePath, string extractionFolderPath)
    {
        using var zip = ZipFile.OpenRead(archiveFilePath);
        bool ShouldBeDecompressed(ZipArchiveEntry entry) => !entry.FullName.EndsWith("\\") && !entry.FullName.EndsWith("/");
        var totalSize = zip.Entries.Where(ShouldBeDecompressed).Sum(x => x.Length);
        var totalCount = zip.Entries.Where(ShouldBeDecompressed).Count();

        string ToPath(string path) => path.ReplaceFirstOccurrence("World of Warcraft", extractionFolderPath);

        var progress = new ProgressBarPrinter("Decompress");
        Console.WriteLine($"Total size to decompress {UtilHelper.ToHumanFileSize(totalSize)}");
        long alreadyDecompressedSize = 0;
        long alreadyDecompressedCount = 0;
        foreach (var entry in zip.Entries.Where(ShouldBeDecompressed))
        {
            var destPath = ToPath(entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            entry.ExtractToFile(destPath, overwrite: true);
            alreadyDecompressedSize += entry.Length;
            alreadyDecompressedCount++;
            progress.UpdateState((alreadyDecompressedSize / (double)(totalSize)), $"{alreadyDecompressedCount}/{totalCount}".PadLeft(3+1+3));
        }
        progress.Done();
    }

#if !PLATFORM_WINDOWS
    private static void Decompress7ZWithProgress(string archiveFilePath, string extractionFolderPath)
    {
        throw new NotSupportedException("7z is only supported on Windows");
    }
#else
    private static void Decompress7ZWithProgress(string archiveFilePath, string extractionFolderPath)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "WinterspringLauncher.7z.dll";

        using (Stream stream = assembly.GetManifestResourceStream(resourceName)!)
        {
            try
            {
                using (var file = File.Open("7z.dll", FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(file);
                }
            }
            catch(Exception e)
            {
                // Maybe the file is somehow already in use
                Console.WriteLine("Failed to write 7z.dll");
                Console.WriteLine(e);
            }
        }

        SevenZipBase.SetLibraryPath("7z.dll");
        string downloadedFile = Path.Combine(archiveFilePath);
        Console.WriteLine($"Extracting archive into {extractionFolderPath}");
        using (var archiveFile = new SevenZipExtractor(downloadedFile))
        {
            var decompressProgress = new ProgressBarPrinter("Decompress");
            bool ShouldBeDecompressed(ArchiveFileInfo entry) => !entry.IsDirectory;
            string ToPath(string path) => path.ReplaceFirstOccurrence("World of Warcraft", extractionFolderPath);

            long totalSize = 0;
            long totalCount = 0;
            foreach (var entry in archiveFile.ArchiveFileData)
            {
                if (ShouldBeDecompressed(entry))
                {
                    totalSize += (long) entry.Size;
                    totalCount++;
                }
            }
            Console.WriteLine($"Total size to decompress {UtilHelper.ToHumanFileSize(totalSize)}");

            long alreadyDecompressedSize = 0;
            long alreadyDecompressedCount = 0;
            foreach (var entry in archiveFile.ArchiveFileData)
            {
                if (ShouldBeDecompressed(entry))
                {
                    var destName = ToPath(entry.FileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(destName)!);
                    using (var fStream = File.Open(destName, FileMode.Create, FileAccess.Write))
                    {
                        archiveFile.ExtractFile(entry.FileName, fStream);
                    }
                    alreadyDecompressedSize += (long) entry.Size;
                    alreadyDecompressedCount++;
                    decompressProgress.UpdateState((alreadyDecompressedSize / (double)(totalSize)), $"{alreadyDecompressedCount}/{totalCount}".PadLeft(3+1+3));
                }
            }
            decompressProgress.Done();
        }
    }
#endif
    public static void DecompressSmartSkipFirstFolder(string zipFilePath, string outputDirectory)
    {
        using var zip = ZipFile.OpenRead(zipFilePath); 
        string? zipBaseFolder = GetBaseFolderFromZip(zip);

#if DEBUG
        Console.WriteLine($"Unzipping {zipFilePath}, detected '{zipBaseFolder ?? "<null>"}' as first folder");
#endif
        
        string ToFilteredPath(string path) => zipBaseFolder != null
            ? path.ReplaceFirstOccurrence(zipBaseFolder, outputDirectory)
            : path;

        string GetCompletePath(ZipArchiveEntry entry) => Path.Combine(outputDirectory, ToFilteredPath(entry.FullName));

        foreach (var entry in zip.Entries.Where(e => !e.IsFolder()))
        {
            var completePath = GetCompletePath(entry);
            Directory.CreateDirectory(Path.GetDirectoryName(completePath)!);
            entry.ExtractToFile(completePath, overwrite: true);
        }
    }

    private static bool IsFolder(this ZipArchiveEntry entry)
    {
        return entry.FullName.EndsWith("/");
    }

    static string? GetBaseFolderFromZip(ZipArchive archive)
    {
        string[] entryPaths = archive.Entries.Select(entry => entry.FullName).ToArray();
        if (entryPaths.Length == 0)
            return null;

        string[] parts = entryPaths[0].Split('/');

        for (int i = 1; i < entryPaths.Length; i++)
        {
            string[] currentParts = entryPaths[i].Split('/');

            int commonParts = parts.Zip(currentParts, (p1, p2) => p1 == p2).TakeWhile(b => b).Count();
            if (commonParts == 0)
                return null;

            Array.Resize(ref parts, commonParts);
        }

        return string.Join("/", parts);
    }
}
