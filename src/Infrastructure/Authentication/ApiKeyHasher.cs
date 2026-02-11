using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Authentication;

public class ApiKeyHasher
{
    public static string HashKey(string apiKey)
    {
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    public static bool VerifyKey(string apiKey, string hashedKey)
    {
        var computedHash = HashKey(apiKey);
        return computedHash == hashedKey;
    }
}