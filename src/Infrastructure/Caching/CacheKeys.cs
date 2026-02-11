using System.Buffers;
using System.IO.Hashing;
using System.Text;

namespace Infrastructure.Caching;

/// <summary>
///     Centralized cache key generator for consistent naming across the application.
///     Format: {entity}:{identifier} for single-tenant
///     Format: {entity}:{projectId}:{identifier} for multi-tenant
/// </summary>
public static class CacheKeys
{
    // API Key cache keys
    public static string ApiKey(string hashedKey)
    {
        return $"apikey:{hashedKey}";
    }

    public static string ApiKeyById(Guid id)
    {
        return $"apikey:id:{id}";
    }

    public static string ApiKeysByProject(Guid projectId)
    {
        return $"apikey:project:{projectId}";
    }

    // Project cache keys
    public static string Project(Guid projectId)
    {
        return $"project:{projectId}";
    }

    public static string ProjectByName(string projectName)
    {
        return $"project:name:{projectName.Trim().ToUpperInvariant()}";
    }

    public static string AllProjects()
    {
        return "project:all";
    }

    // Feature Flag cache keys
    public static string Flag(Guid projectId, Guid flagId)
    {
        return $"flag:{projectId}:{flagId}";
    }

    public static string FlagByKey(Guid projectId, string flagKey)
    {
        return $"flag:{projectId}:key:{flagKey}";
    }

    public static string FlagsByProject(Guid projectId)
    {
        return $"flag:project:{projectId}";
    }

    // Evaluation cache keys
    public static string Evaluation(Guid projectId, string flagKey, string userId, int flagVersion,
        ulong? contextHash = null)
    {
        var baseKey = $"eval:{projectId}:{flagKey}:v{flagVersion}:{userId}";
        return contextHash != null ? $"{baseKey}:{contextHash}" : baseKey;
    }

    /// <summary>
    ///     Generate a hash for complex evaluation contexts to use in cache keys.
    /// </summary>
    public static ulong HashContext(IEnumerable<string>? groups)
    {
        if (groups is null)
            return 0;

        var arr = groups as string[] ?? groups.ToArray();
        if (arr.Length == 0)
            return 0;

        Array.Sort(arr, StringComparer.Ordinal);

        return HashGroups(arr);
    }

    private static ulong HashGroups(ReadOnlySpan<string> groups)
    {
        if (groups.Length == 0)
            return 0;

        var hasher = new XxHash64();

        // Domain separation
        hasher.Append("|groups"u8);

        foreach (var g in groups)
        {
            hasher.Append(","u8);

            var maxBytes = Encoding.UTF8.GetMaxByteCount(g.Length);

            if (maxBytes <= 256)
            {
                Span<byte> buffer = stackalloc byte[256];
                var written = Encoding.UTF8.GetBytes(g, buffer);
                hasher.Append(buffer[..written]);
            }
            else
            {
                var rented = ArrayPool<byte>.Shared.Rent(maxBytes);
                try
                {
                    var written = Encoding.UTF8.GetBytes(g, rented);
                    hasher.Append(rented.AsSpan(0, written));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        return hasher.GetCurrentHashAsUInt64();
    }
}