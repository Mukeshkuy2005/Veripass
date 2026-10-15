using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace VeriPass.Infrastructure.Storage;

public interface ISignedUrlService
{
    string GenerateSignedUrl(string storageKey, TimeSpan expiry);
    bool ValidateSignedUrl(string storageKey, long expires, string signature);
}

public class SignedUrlService : ISignedUrlService
{
    private readonly byte[] _secretKey;

    public SignedUrlService(IConfiguration configuration)
    {
        var secret = configuration["Storage:SigningSecret"] ?? "VERIPASS_DOCUMENT_SECURE_SIGNING_KEY_2026_HMAC_SHA256";
        _secretKey = Encoding.UTF8.GetBytes(secret);
    }

    public string GenerateSignedUrl(string storageKey, TimeSpan expiry)
    {
        var expires = DateTimeOffset.UtcNow.Add(expiry).ToUnixTimeSeconds();
        var signature = ComputeSignature(storageKey, expires);
        return $"/api/v1/documents/download/{storageKey}?expires={expires}&sig={signature}";
    }

    public bool ValidateSignedUrl(string storageKey, long expires, string signature)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires)
            return false;

        var expectedSignature = ComputeSignature(storageKey, expires);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature),
            Encoding.UTF8.GetBytes(expectedSignature));
    }

    private string ComputeSignature(string storageKey, long expires)
    {
        var payload = $"{storageKey}:{expires}";
        using var hmac = new HMACSHA256(_secretKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
