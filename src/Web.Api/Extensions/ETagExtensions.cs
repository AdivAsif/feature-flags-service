using System.Security.Cryptography;
using System.Text;
using Application.DTOs;

namespace Web.Api.Extensions;

public static class ETagExtensions
{
    public static string GenerateETag(this FeatureFlagDTO featureFlag)
    {
        var versionString = $"{featureFlag.Id}-{featureFlag.Version}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(versionString));
        return $"\"{Convert.ToBase64String(hash)}\"";
    }

    extension(HttpRequest request)
    {
        public bool ValidateETag(string expectedETag)
        {
            if (!request.Headers.TryGetValue("If-Match", out var ifMatch))
                return false;

            return ifMatch.ToString() == expectedETag;
        }

        public bool HasIfMatchHeader()
        {
            return request.Headers.ContainsKey("If-Match");
        }
    }
}