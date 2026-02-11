using Application.Interfaces;
using Application.Interfaces.Repositories;
using Contracts.Models;
using Contracts.Responses;
using Infrastructure.Caching;
using ZiggyCreatures.Caching.Fusion;

namespace Infrastructure.Services;

/// <summary>
///     Cached decorator for EvaluationService using FusionCache.
///     Cache Strategy:
///     - Local: 5 minutes (evaluations are hot path but need frequent updates)
///     - Distributed: 30 minutes
///     - Cache key includes flag version for automatic invalidation
///     - Background operations enabled
/// </summary>
public sealed class CachedEvaluationService(
    IEvaluationService innerService,
    IFusionCache cache,
    IFeatureFlagRepository flagRepository) : IEvaluationService
{
    private static readonly FusionCacheEntryOptions CacheOptions = new()
    {
        Duration = TimeSpan.FromMinutes(5),
        DistributedCacheDuration = TimeSpan.FromMinutes(30),
        IsFailSafeEnabled = false,
        FailSafeMaxDuration = TimeSpan.FromHours(1),
        AllowBackgroundDistributedCacheOperations = true,
        Size = 1,

        FactorySoftTimeout = TimeSpan.FromMilliseconds(100),
        FactoryHardTimeout = TimeSpan.FromMilliseconds(500),
        EagerRefreshThreshold = null
    };

    public async Task<EvaluationResponse> EvaluateAsync(Guid projectId, string featureFlagKey,
        EvaluationContext context, CancellationToken cancellationToken = default)
    {
        // Fetch flag to get version for cache key. 
        // Note: flagRepository is decorated with CachedFeatureFlagRepository, 
        // so this is a fast L1/L2 lookup.
        var flag = await flagRepository.GetByKeyAsync(projectId, featureFlagKey, cancellationToken);

        // If flag doesn't exist, no caching needed - return immediately
        if (flag == null)
            return await innerService.EvaluateAsync(projectId, featureFlagKey, context, cancellationToken);

        var contextHash = CacheKeys.HashContext(context.Groups);
        var cacheKey = CacheKeys.Evaluation(projectId, featureFlagKey, context.UserId, flag.Version, contextHash);

        return await cache.GetOrSetAsync(
            cacheKey,
            // Optimization: Pass the already-fetched flag to avoid double DB lookup
            async ct => await innerService.EvaluateAsync(flag, context, ct),
            CacheOptions,
            cancellationToken);
    }

    public Task<EvaluationResponse> EvaluateAsync(Domain.FeatureFlag featureFlag, EvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        // Direct pass-through for the overload, though typically the cached path enters via key
        return innerService.EvaluateAsync(featureFlag, context, cancellationToken);
    }
}