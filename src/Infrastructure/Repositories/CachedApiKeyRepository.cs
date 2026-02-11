using Domain;
using ZiggyCreatures.Caching.Fusion;

namespace Infrastructure.Repositories;

/// <summary>
///     Cached decorator for ApiKeyRepository using FusionCache.
///     Dramatically reduces database load for API key validation.
/// </summary>
public class CachedApiKeyRepository(IApiKeyRepository innerRepository, IFusionCache cache) : IApiKeyRepository
{
    private const int CacheDurationMinutes = 5;

    public Task<ApiKey?> GetByIdAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        return innerRepository.GetByIdAsync(apiKeyId, cancellationToken);
    }

    /// <summary>
    ///     Get API key by hash with caching.
    ///     This is the HOT PATH for authentication - called on every request.
    ///     Cache hit: ~1-2ms, Cache miss: ~10-20ms
    /// </summary>
    public async Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"ApiKey:hash:{keyHash}";

        var apiKey = await cache.GetOrSetAsync(
            cacheKey,
            async _ => await innerRepository.GetByKeyHashAsync(keyHash, cancellationToken),
            options => options
                .SetDuration(TimeSpan.FromMinutes(CacheDurationMinutes))
                .SetFailSafe(true), // Return stale data if DB is down
            cancellationToken);

        return apiKey;
    }

    public async Task<IEnumerable<ApiKey>> GetByProjectIdAsync(Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"ApiKey:project:{projectId}";

        var apiKeys = await cache.GetOrSetAsync(
            cacheKey,
            async _ => await innerRepository.GetByProjectIdAsync(projectId, cancellationToken),
            options => options.SetDuration(TimeSpan.FromMinutes(1)), // Shorter cache for list
            cancellationToken);

        return apiKeys;
    }

    public async Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        var createdKey = await innerRepository.CreateAsync(apiKey, cancellationToken);

        // Cache the newly created key immediately
        await cache.SetAsync($"ApiKey:hash:{createdKey.KeyHash}", createdKey,
            options => options.SetDuration(TimeSpan.FromMinutes(CacheDurationMinutes)),
            cancellationToken);

        // Invalidate project list cache
        await cache.RemoveAsync($"ApiKey:project:{createdKey.ProjectId}", token: cancellationToken);

        return createdKey;
    }

    public async Task RevokeAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        // Get the key first to find its hash for cache invalidation.
        // This is a trade-off: extra DB query but ensures cache consistency.
        var apiKey = await innerRepository.GetByIdAsync(apiKeyId, cancellationToken);

        await innerRepository.RevokeAsync(apiKeyId, cancellationToken);

        // Invalidate cache immediately - security critical!
        if (apiKey != null)
        {
            await cache.RemoveAsync($"ApiKey:hash:{apiKey.KeyHash}", token: cancellationToken);
            await cache.RemoveAsync($"ApiKey:project:{apiKey.ProjectId}", token: cancellationToken);
        }
    }

    public Task UpdateLastUsedAtAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        return innerRepository.UpdateLastUsedAtAsync(apiKeyId, cancellationToken);
    }
}