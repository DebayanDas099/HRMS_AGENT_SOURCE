using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HRMS_CHATBOT_SOURCE.Agent;

public static class DocumentDownloadTokenCodec
{
    private static readonly byte[] Salt = [0x49, 0x76, 0x61, 0x6E, 0x20, 0x4D, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76];

    public static string Encrypt(string? mobile, long documentId, string? encryptionKey)
    {
        if (documentId <= 0 || string.IsNullOrWhiteSpace(encryptionKey))
        {
            return string.Empty;
        }

        var payloadJson = JsonSerializer.Serialize(new TokenPayload(mobile?.Trim() ?? string.Empty, documentId));
        var clearBytes = Encoding.UTF8.GetBytes(payloadJson);

        using var aes = Aes.Create();
        using var pdb = new Rfc2898DeriveBytes(encryptionKey, Salt, 1000, HashAlgorithmName.SHA256);
        aes.Key = pdb.GetBytes(32);
        aes.IV = pdb.GetBytes(16);

        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            cs.Write(clearBytes, 0, clearBytes.Length);
            cs.FlushFinalBlock();
        }

        return Uri.EscapeDataString(Convert.ToBase64String(ms.ToArray()));
    }

    public static bool TryDecrypt(string? token, string? encryptionKey, out string mobile, out long documentId)
    {
        mobile = string.Empty;
        documentId = 0;

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(encryptionKey))
        {
            return false;
        }

        try
        {
            var normalized = Uri.UnescapeDataString(token).Replace(" ", "+", StringComparison.Ordinal);
            var cipherBytes = Convert.FromBase64String(normalized);

            using var aes = Aes.Create();
            using var pdb = new Rfc2898DeriveBytes(encryptionKey, Salt, 1000, HashAlgorithmName.SHA256);
            aes.Key = pdb.GetBytes(32);
            aes.IV = pdb.GetBytes(16);

            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
            {
                cs.Write(cipherBytes, 0, cipherBytes.Length);
                cs.FlushFinalBlock();
            }

            var clearText = Encoding.UTF8.GetString(ms.ToArray());
            var payload = JsonSerializer.Deserialize<TokenPayload>(clearText);
            if (payload == null || payload.DocumentId <= 0)
            {
                return false;
            }

            mobile = payload.Mobile ?? string.Empty;
            documentId = payload.DocumentId;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed record TokenPayload(string Mobile, long DocumentId);
}
