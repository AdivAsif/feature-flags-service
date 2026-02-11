using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Caching;
using Infrastructure.Services;
using ZiggyCreatures.Caching.Fusion;

namespace Infrastructure.Repositories;

/// <summary>
///     Cached decorator for ApiKeyRepository using FusionCache.
///     Cache Strategy:
///     - Local: 4 hours (API keys rarely change)
///     - Distributed: 24 hours
///     - Background operations enabled for write-through caching
/// </summary>
public class CachedApiKeyRepository : IApiKeyRepository
{
    private static readonly FusionCacheEntryOptions CacheOptions = new()
    {
        Duration = TimeSpan.FromHours(4),
        DistributedCacheDuration = TimeSpan.FromHours(24),
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromHours(48),
        AllowBackgroundDistributedCacheOperations = true,
        Size = 1
    };

    private readonly IFusionCache _cache;
    private readonly IApiKeyRepository _innerRepository;
    private readonly ApiKeyUsageQueue _usageQueue;

    public CachedApiKeyRepository(IApiKeyRepository innerRepository, IFusionCache cache, ApiKeyUsageQueue usageQueue)
    {
        _innerRepository = innerRepository;
        _cache = cache;
        _usageQueue = usageQueue;
    }

    public async Task<ApiKey?> GetByIdAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.ApiKeyById(apiKeyId);

        return await _cache.GetOrSetAsync(
            cacheKey,
            async _ => await _innerRepository.GetByIdAsync(apiKeyId, cancellationToken),
            CacheOptions,
            cancellationToken);
    }

    /// <summary>
    ///     Get API key by hash with caching.
    ///     This is the HOT PATH for authentication - called on every request.
    /// </summary>
    public async Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.ApiKey(keyHash);

        return await _cache.GetOrSetAsync(
            cacheKey,
            async _ => await _innerRepository.GetByKeyHashAsync(keyHash, cancellationToken),
            CacheOptions,
            cancellationToken);
    }

    public async Task<IEnumerable<ApiKey>> GetByProjectIdAsync(Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.ApiKeysByProject(projectId);

        return await _cache.GetOrSetAsync(
            cacheKey,
            async _ => await _innerRepository.GetByProjectIdAsync(projectId, cancellationToken),
            new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMinutes(30),
                DistributedCacheDuration = TimeSpan.FromHours(2),
                AllowBackgroundDistributedCacheOperations = true,
                Size = 1
            },
            cancellationToken);
    }

    public async Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken cancellationToken = default)
    {
        var createdKey = await _innerRepository.CreateAsync(apiKey, cancellationToken);

        _ = Task.Run(async () =>
        {
            await _cache.SetAsync(CacheKeys.ApiKey(createdKey.KeyHash), createdKey, CacheOptions, cancellationToken);
            await _cache.SetAsync(CacheKeys.ApiKeyById(createdKey.Id), createdKey, CacheOptions, cancellationToken);
            await _cache.RemoveAsync(CacheKeys.ApiKeysByProject(createdKey.ProjectId), token: cancellationToken);
        }, cancellationToken);

        return createdKey;
    }

    public async Task RevokeAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        var apiKey = await _innerRepository.GetByIdAsync(apiKeyId, cancellationToken);
        await _innerRepository.RevokeAsync(apiKeyId, cancellationToken);

        if (apiKey != null)
        {
            await _cache.RemoveAsync(CacheKeys.ApiKey(apiKey.KeyHash), token: cancellationToken);
            await _cache.RemoveAsync(CacheKeys.ApiKeyById(apiKey.Id), token: cancellationToken);
            await _cache.RemoveAsync(CacheKeys.ApiKeysByProject(apiKey.ProjectId), token: cancellationToken);
        }
    }

    public async Task UpdateLastUsedAtAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        // Queue for background processing instead of blocking the request
        // The background service will throttle updates to once per hour per key
        _ = _usageQueue.QueueApiKeyUsageAsync(apiKeyId, cancellationToken);
    }
}