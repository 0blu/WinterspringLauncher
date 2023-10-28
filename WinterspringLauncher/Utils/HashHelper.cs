using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WinterspringLauncher.Utils;

public class HashHelper
{
    public static string CreateHexSha256HashFromFilename(string filePath)
    {
        using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(stream);
            return ConvertBinarySha256ToHex(hashBytes);
        }
    }

    public static string CreateHexSha256HashFromFileBytes(byte[] fileContent)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = sha256.ComputeHash(fileContent);
            return ConvertBinarySha256ToHex(hashBytes);
        }
    }

    public static string ConvertBinarySha256ToHex(byte[] binarySha256Hash)
    {
        if (binarySha256Hash.Length != 32)
            throw new ArgumentException("Expected a 32byte long Sha256 hash");
        
        StringBuilder hashBuilder = new StringBuilder(32);
        foreach (byte b in binarySha256Hash)
            hashBuilder.Append(b.ToString("x2"));
        return hashBuilder.ToString();
    }
}
