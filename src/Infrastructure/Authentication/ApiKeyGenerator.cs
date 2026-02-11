using System.Security.Cryptography;

namespace Infrastructure.Authentication;

public class ApiKeyGenerator
{
    private const int KeyLength = 32;

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
            .Replace("=", "")
            .Substring(0, 32);

        return $"{prefix}{base64}";
    }

    public static string ExtractPrefix(string apiKey)
    {
        var prefixEndIndex = apiKey.LastIndexOf('_');
        return prefixEndIndex > 0 ? apiKey.Substring(0, prefixEndIndex + 1) : string.Empty;
    }
}