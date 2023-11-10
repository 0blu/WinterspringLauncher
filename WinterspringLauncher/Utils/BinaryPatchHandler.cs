using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace WinterspringLauncher.Utils;

public class BinaryPatchHandler
{
    public class PatchSummary
    {
        [JsonPropertyName("windows")] 
        public PatchSummaryEntry? Windows { get; set; }

        [JsonPropertyName("macos")] 
        public PatchSummaryEntry? MacOs { get; set; }
        
        public class PatchSummaryEntry
        {
            [JsonPropertyName("from_sha256")] 
            public string FromSha256 { get; set; } = null!;

            [JsonPropertyName("to_sha256")] 
            public string ToSha256 { get; set; } = null!;

            [JsonPropertyName("last_update")] 
            public ulong LastUpdate { get; set; } = 0;

            [JsonPropertyName("patch_filename")]
            public string PatchFilename { get; set; } = null!;
        }
    }

    public static void ApplyPatch(byte[] patchFileContent, string sourceFile, string targetFile)
    {
        // Read header information
        byte[] magic = patchFileContent.Take(4).ToArray();
        if (!magic.SequenceEqual(new byte[] { 0x42, 0x42, 0x50, 0x31 }))
            throw new ArgumentException("Invalid patch file format (expected BBP1)");

        // Verifying signature
        {
            byte[] everythingButSignature = patchFileContent.SkipLast(256).ToArray();
            byte[] signature = patchFileContent.TakeLast(256).ToArray();

            VerifySignatureOrThrow(everythingButSignature, signature);
        }

        byte[] fileBytes = File.ReadAllBytes(sourceFile);

        string expectedOriginalHash = HashHelper.ConvertBinarySha256ToHex(patchFileContent.Skip(4).Take(32).ToArray());
        string actualOriginalHash = HashHelper.CreateHexSha256HashFromFileBytes(fileBytes);
        if (actualOriginalHash != expectedOriginalHash)
            throw new Exception($"Cannot apply patch because the hash of source file is incorrect. Expected '{expectedOriginalHash}' Actual: '{actualOriginalHash}'");

        string expectedHashAfterPatch = HashHelper.ConvertBinarySha256ToHex(patchFileContent.Skip(36).Take(32).ToArray());
        ulong patchCount = BitConverter.ToUInt64(patchFileContent, startIndex: 68);

        long currentPosition = 76; // Start after the header
        ulong patchesApplied = 0;

        for (ulong patchEntryIdx = 0; patchEntryIdx < patchCount; patchEntryIdx++)
        {
            if (currentPosition + 12 > patchFileContent.Length)
            {
                throw new ArgumentException("Patch file is incomplete");
            }

            int fileOffset = (int)BitConverter.ToUInt64(patchFileContent, (int)currentPosition);
            uint patchSize = BitConverter.ToUInt32(patchFileContent, (int)(currentPosition + 8));

            currentPosition += 12;

            // If the file offset is out of bounds, extend the file and initialize with 0x00
            if (fileOffset > fileBytes.Length)
                Array.Resize(ref fileBytes, (int)(fileOffset + patchSize));

            // Apply the patch to the source file
            for (int patchByteIdx = 0; patchByteIdx < patchSize; patchByteIdx++)
                fileBytes[fileOffset + patchByteIdx] = patchFileContent[currentPosition + patchByteIdx];
            
            currentPosition += patchSize;

            patchesApplied++;
        }

        if (patchesApplied != patchCount)
            throw new InvalidOperationException("Not all patches were applied");

        // Verify the integrity of the patched file
        string actualHashAfterPatch = HashHelper.CreateHexSha256HashFromFileBytes(fileBytes);
        if (actualHashAfterPatch != expectedHashAfterPatch)
            throw new Exception($"Invalid patch result. Expected '{expectedHashAfterPatch}' Actual: '{actualHashAfterPatch}'");

        File.WriteAllBytes(targetFile, fileBytes);
    }

    private static void VerifySignatureOrThrow(byte[] bytesToVerify, byte[] signature)
    {
        if (signature.Length != 256)
            throw new ArgumentException("Signature must be 256 bytes long");

        // ref https://wow-patches.blu.wtf/sign_key.pub
        const string publicKey = @"
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAmy/cX6/VOlOpgLnQWnWS
tFVqf9xAO2uNjeSeUHmiMTQTwfm8hnDbcEAz5V4ou987dfDxXZb5WGVxoHnugMS/
rUrOSZ8VQolH+3IanhFNrqRxRTOVk+ZlTrxV9k1iC34kXeoRryiQcqMYLlX4jT3E
EupzAivNsJYm2X/jVGFgPfrDObwOjq23aLdey2uI3YA6SgIg/ayp/YyJEp775lr4
Z+49t3p7WMNZw8VJkQvDB5/t64Bjd9bdIQxsO9jWyHl/z7QOrnAKv0uUPdcCCwWp
kERTaAnq6tK0rAvcYMlJ230cihY+s/7QpIHpsq091La9n4nJCpFIunaaG1JyNHk5
GQIDAQAB
-----END PUBLIC KEY-----
";
        var rsa = new RSACryptoServiceProvider();
        rsa.ImportFromPem(publicKey);

        bool signatureIsValid = rsa.VerifyData(bytesToVerify, signature, hashAlgorithm: HashAlgorithmName.SHA256, padding: RSASignaturePadding.Pkcs1);
        if (!signatureIsValid)
            throw new Exception("Signature not valid");
    }
}
