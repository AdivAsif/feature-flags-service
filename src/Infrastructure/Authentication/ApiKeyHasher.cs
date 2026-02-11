using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Authentication;

public static class ApiKeyHasher
{
    // API Keys are more mission-critical than other places where I use xxHash, so I want to use a stronger hashing
    // algorithm (SHA256) to minimize the risk of collisions and enhance security
    public static string HashKey(string apiKey)
    {
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}