using Application.Common;
using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Caching;
using ZiggyCreatures.Caching.Fusion;

namespace Infrastructure.Repositories;

/// <summary>
///     Cached decorator for FeatureFlagsRepository using FusionCache.
///     Cache Strategy:
///     - Local: 10 minutes (flags change moderately)
///     - Distributed: 1 hour
///     - Eager refresh at 90% (proactive refresh before expiry)
///     - Background operations enabled
/// </summary>
public class CachedFeatureFlagRepository(IFeatureFlagRepository innerRepository, IFusionCache cache)
    : IFeatureFlagRepository
{
    private static readonly FusionCacheEntryOptions CacheOptions = new()
    {
        Duration = TimeSpan.FromMinutes(10),
        DistributedCacheDuration = TimeSpan.FromHours(1),
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromHours(2),
        EagerRefreshThreshold = 0.9f,
        AllowBackgroundDistributedCacheOperations = true,
        Size = 1
    };

    public async Task<FeatureFlag?> GetByIdAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.Flag(projectId, id);

        return await cache.GetOrSetAsync(
            cacheKey,
            async ct => await innerRepository.GetByIdAsync(projectId, id, ct),
            CacheOptions,
            cancellationToken);
    }

    public async Task<FeatureFlag?> GetByKeyAsync(Guid projectId, string key,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.FlagByKey(projectId, key);

        return await cache.GetOrSetAsync(
            cacheKey,
            async ct => await innerRepository.GetByKeyAsync(projectId, key, ct),
            CacheOptions,
            cancellationToken);
    }

    public Task<Slice<FeatureFlag>> GetPagedAsync(Guid projectId, int first = 10, string? after = null,
        string? before = null, CancellationToken cancellationToken = default)
    {
        return innerRepository.GetPagedAsync(projectId, first, after, before, cancellationToken);
    }

    public async Task<FeatureFlag> CreateAsync(FeatureFlag featureFlag, CancellationToken cancellationToken = default)
    {
        var created = await innerRepository.CreateAsync(featureFlag, cancellationToken);

        await cache.SetAsync(CacheKeys.Flag(created.ProjectId, created.Id), created, CacheOptions,
            token: cancellationToken);
        await cache.SetAsync(CacheKeys.FlagByKey(created.ProjectId, created.Key), created, CacheOptions,
            token: cancellationToken);
        await cache.RemoveAsync(CacheKeys.FlagsByProject(created.ProjectId), token: cancellationToken);

        return created;
    }

    public async Task<FeatureFlag> UpdateAsync(FeatureFlag featureFlag, CancellationToken cancellationToken = default)
    {
        var updated = await innerRepository.UpdateAsync(featureFlag, cancellationToken);

        await cache.SetAsync(CacheKeys.Flag(updated.ProjectId, updated.Id), updated, CacheOptions,
            token: cancellationToken);
        await cache.SetAsync(CacheKeys.FlagByKey(updated.ProjectId, updated.Key), updated, CacheOptions,
            token: cancellationToken);
        await cache.RemoveAsync(CacheKeys.FlagsByProject(updated.ProjectId), token: cancellationToken);

        return updated;
    }

    public async Task DeleteAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default)
    {
        var flag = await innerRepository.GetByIdAsync(projectId, id, cancellationToken);

        await innerRepository.DeleteAsync(projectId, id, cancellationToken);

        if (flag != null)
        {
            await cache.RemoveAsync(CacheKeys.Flag(flag.ProjectId, flag.Id), token: cancellationToken);
            await cache.RemoveAsync(CacheKeys.FlagByKey(flag.ProjectId, flag.Key), token: cancellationToken);
            await cache.RemoveAsync(CacheKeys.FlagsByProject(flag.ProjectId), token: cancellationToken);
        }
    }
}