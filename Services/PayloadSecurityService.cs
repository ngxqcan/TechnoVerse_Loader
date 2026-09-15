using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TechnoVerseLoader.Services
{
    public class DecryptedPayloadResult
    {
        public string OriginalFileName { get; set; } = "";
        public string OriginalExtension { get; set; } = "";
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }

    public static class PayloadSecurityService
    {
        public const string DefaultPassword = "TechnoVerse@2026";
        private static readonly byte[] MagicBytes = Encoding.UTF8.GetBytes("TVBIN1"); // 6 bytes

        public static bool IsEncryptedPayload(byte[] data)
        {
            if (data == null || data.Length < 50) return false;
            for (int i = 0; i < 6; i++)
            {
                if (data[i] != MagicBytes[i]) return false;
            }
            return true;
        }

        public static bool IsEncryptedPayloadFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length < 50) return false;
                byte[] header = new byte[6];
                int read = fs.Read(header, 0, 6);
                if (read < 6) return false;
                for (int i = 0; i < 6; i++)
                {
                    if (header[i] != MagicBytes[i]) return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static DecryptedPayloadResult DecryptPayload(byte[] fileBytes, string? password = null)
        {
            string pass = string.IsNullOrWhiteSpace(password) ? DefaultPassword : password.Trim();

            if (!IsEncryptedPayload(fileBytes))
            {
                throw new InvalidOperationException("Tệp tin không phải là TechnoVerse Payload hợp lệ.");
            }

            // [Magic 6] [Salt 16] [IV 12] [Tag 16] [CipherText ...]
            ReadOnlySpan<byte> salt = fileBytes.AsSpan(6, 16);
            ReadOnlySpan<byte> iv = fileBytes.AsSpan(22, 12);
            ReadOnlySpan<byte> tag = fileBytes.AsSpan(34, 16);

            int cipherTextLen = fileBytes.Length - 50;
            ReadOnlySpan<byte> cipherText = fileBytes.AsSpan(50, cipherTextLen);

            byte[] key = new byte[32];
            Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(pass),
                salt,
                key,
                10000,
                HashAlgorithmName.SHA256
            );

            byte[] plainText = new byte[cipherTextLen];
            using (var aesGcm = new AesGcm(key, 16))
            {
                aesGcm.Decrypt(iv, cipherText, tag, plainText);
            }

            // Parse header
            int metaLen = BitConverter.ToInt32(plainText, 0);
            string metaJson = Encoding.UTF8.GetString(plainText, 4, metaLen);
            using var doc = JsonDocument.Parse(metaJson);
            string origName = doc.RootElement.GetProperty("name").GetString() ?? "payload.exe";
            string origExt = doc.RootElement.GetProperty("ext").GetString() ?? Path.GetExtension(origName);
            bool isCompressed = doc.RootElement.TryGetProperty("compressed", out var compProp) && compProp.GetBoolean();

            int compressedOffset = 4 + metaLen;
            int compressedLength = plainText.Length - compressedOffset;

            byte[] rawBytes;
            if (isCompressed)
            {
                using var compressedMs = new MemoryStream(plainText, compressedOffset, compressedLength);
                using var deflateStream = new DeflateStream(compressedMs, CompressionMode.Decompress);
                using var decompressedMs = new MemoryStream();
                deflateStream.CopyTo(decompressedMs);
                rawBytes = decompressedMs.ToArray();
            }
            else
            {
                rawBytes = new byte[compressedLength];
                Buffer.BlockCopy(plainText, compressedOffset, rawBytes, 0, compressedLength);
            }

            return new DecryptedPayloadResult
            {
                OriginalFileName = origName,
                OriginalExtension = origExt,
                Data = rawBytes
            };
        }
    }
}