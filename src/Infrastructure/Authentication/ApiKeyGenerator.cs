using System.Security.Cryptography;

namespace Infrastructure.Authentication;

public static class ApiKeyGenerator
{
    private const int KeyLength = 32;

    // Basic generator for API Keys, ffsk because feature flags service key, live for production
    public static string GenerateKey(string prefix = "ffsk_live_")
    {
        var randomBytes = new byte[KeyLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        var base64 = Convert.ToBase64String(randomBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")[..32];

        return $"{prefix}{base64}";
    }

    public static string ExtractPrefix(string apiKey)
    {
        var prefixEndIndex = apiKey.LastIndexOf('_');
        return prefixEndIndex > 0 ? apiKey[..(prefixEndIndex + 1)] : string.Empty;
    }
}