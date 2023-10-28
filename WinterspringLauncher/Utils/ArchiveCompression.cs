using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;

#if PLATFORM_WINDOWS
using SevenZip;
#endif

namespace WinterspringLauncher.Utils;

public static class ArchiveCompression
{
    public delegate void UnpackProgressInfoHandler(long totalFileCount, long alreadyUnpackedFileCount);

    public static void Decompress(string archiveFilePath, string extractionFolderPath, string folderToSkipName, UnpackProgressInfoHandler progressHandler)
    {
        byte[] buffer = new byte[4];
        using (FileStream fileHandle = File.OpenRead(archiveFilePath))
        {
            fileHandle.Read(buffer, 0, 4);
        }

        if (buffer.SequenceEqual(new byte[] { 0x52, 0x61, 0x72, 0x21 })) // Rar!
        {
            Decompress7ZWithProgress(archiveFilePath, extractionFolderPath, folderToSkipName, progressHandler);
        }
        else if (buffer[..2].SequenceEqual(new byte[] { 0x50, 0x4B })) // Zip
        {
            DecompressZipWithProgress(archiveFilePath, extractionFolderPath, folderToSkipName, progressHandler);
        }
        else // Error
        {
            throw new Exception("Unknown file format. Cannot decompress");
        }
    }

    private static void DecompressZipWithProgress(string archiveFilePath, string extractionFolderPath, string folderToSkipName, UnpackProgressInfoHandler progressHandler)
    {
        using var zip = ZipFile.OpenRead(archiveFilePath);
        bool ShouldBeDecompressed(ZipArchiveEntry entry) => !entry.FullName.EndsWith("\\") && !entry.FullName.EndsWith("/");
        var totalSize = zip.Entries.Where(ShouldBeDecompressed).Sum(x => x.Length);
        var totalCount = zip.Entries.Where(ShouldBeDecompressed).Count();

        string ToPath(string path) => Path.Combine(extractionFolderPath, path);

        Console.WriteLine($"Total size to decompress {UtilHelper.ToHumanFileSize(totalSize)}");
        long alreadyDecompressedCount = 0;
        foreach (var entry in zip.Entries.Where(ShouldBeDecompressed))
        {
            var destPath = ToPath(entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            entry.ExtractToFile(destPath, overwrite: true);
            alreadyDecompressedCount++;
            progressHandler(totalCount, alreadyDecompressedCount);
        }
        progressHandler(totalCount, totalCount);
    }

#if !PLATFORM_WINDOWS
    private static void Decompress7ZWithProgress(string archiveFilePath, string extractionFolderPath)
    {
        throw new NotSupportedException("7z is only supported on Windows");
    }
#else
    private static void Decompress7ZWithProgress(string archiveFilePath, string extractionFolderPath, string folderToSkipName, UnpackProgressInfoHandler progressHandler)
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
            bool ShouldBeDecompressed(ArchiveFileInfo entry) => !entry.IsDirectory;
            string ToPath(string path) => path.ReplaceFirstOccurrence(folderToSkipName, extractionFolderPath);

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
                    alreadyDecompressedCount++;
                    progressHandler(totalCount, alreadyDecompressedCount);
                }
            }
            progressHandler(totalCount, totalCount);
        }

        try
        {
            File.Delete("7z.dll");
        }
        catch
        {
            // ignored
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
