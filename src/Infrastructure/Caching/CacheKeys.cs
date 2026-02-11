using System.Buffers;
using System.IO.Hashing;
using System.Text;
using Cysharp.Text;

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
        return ZString.Concat("apikey:", hashedKey);
    }

    public static string ApiKeyById(Guid id)
    {
        return ZString.Concat("apikey:id:", id);
    }

    public static string ApiKeysByProject(Guid projectId)
    {
        return ZString.Concat("apikey:project:", projectId);
    }

    // Project cache keys
    public static string Project(Guid projectId)
    {
        return ZString.Concat("project:", projectId);
    }

    public static string ProjectByName(string projectName)
    {
        return ZString.Concat("project:name:", projectName.Trim().ToUpperInvariant());
    }

    public static string AllProjects()
    {
        return "project:all";
    }

    // Feature Flag cache keys
    public static string Flag(Guid projectId, Guid flagId)
    {
        return ZString.Concat("flag:", projectId, ":", flagId);
    }

    public static string FlagByKey(Guid projectId, string flagKey)
    {
        return ZString.Concat("flag:", projectId, ":key:", flagKey);
    }

    public static string FlagsByProject(Guid projectId)
    {
        return ZString.Concat("flag:project:", projectId);
    }

    // Evaluation cache keys
    public static string Evaluation(Guid projectId, string flagKey, string userId, int flagVersion,
        ulong? contextHash = null)
    {
        return contextHash != null
            ? ZString.Concat("eval:", projectId, ":", flagKey, ":v", flagVersion, ":", userId, ":", contextHash)
            : ZString.Concat("eval:", projectId, ":", flagKey, ":v", flagVersion, ":", userId);
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