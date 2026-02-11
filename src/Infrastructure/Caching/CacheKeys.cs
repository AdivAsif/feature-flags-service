using System.Buffers;
using System.IO.Hashing;
using System.Text;
using Cysharp.Text;

namespace Infrastructure.Caching;

/// <summary>
///     Centralized cache key generator for consistent naming across the application.
///     Format: {entity}:{identifier} for single-tenant (in this case project)
///     Format: {entity}:{projectId}:{identifier} for multi-tenant (in this case projects)
/// </summary>
public static class CacheKeys
{
    // API Key cache keys
    // Using ZString for efficient concatenation, especially when generating many keys in loops (e.g., multiple flags)
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

    // Projects are not paginated, so we can cache the entire list under a single key, this is more rare than listing 
    // feature flags, so it is affordable to have a single key for all projects
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
    // The context hash is a hash of data like userId, their groups, etc, makes no sense to store as plaintext
    public static string Evaluation(Guid projectId, string flagKey, string userId, int flagVersion,
        ulong? contextHash = null)
    {
        return contextHash != null
            ? ZString.Concat("eval:", projectId, ":", flagKey, ":v", flagVersion, ":", userId, ":", contextHash)
            : ZString.Concat("eval:", projectId, ":", flagKey, ":v", flagVersion, ":", userId);
    }
    
    // Generate a hash for complex evaluation contexts to use in cache keys, ulong instead of strings -> 8 bytes vs.
    // potentially much more with strings
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

        // Using XxHash64 for fast, non-cryptographic hashing. We only need to generate a hash for the groups, which is
        // the most complex part of the context, and can be variable in length. This allows us to have a fixed-size
        // representation (ulong) for the groups in our cache keys, which is more efficient than using a string
        // representation of the groups and results in far smaller (better) cache key sizes. It also avoids CPU 
        // overhead with the tradeoff of cryptographic guarantees - not particularly important for cache keys in this
        // context
        var hasher = new XxHash64();

        // Domain separation using delimiter
        hasher.Append("|groups"u8);

        foreach (var g in groups)
        {
            hasher.Append(","u8);

            var maxBytes = Encoding.UTF8.GetMaxByteCount(g.Length);

            if (maxBytes <= 256)
            {
                // For small strings, we can avoid allocating on the heap and use stack allocation for better performance
                Span<byte> buffer = stackalloc byte[256];
                var written = Encoding.UTF8.GetBytes(g, buffer);
                hasher.Append(buffer[..written]);
            }
            else
            {
                // Rent to avoid GC pressure for larger strings, this is less performant than stackalloc but necessary
                // for larger inputs to avoid memory issues
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