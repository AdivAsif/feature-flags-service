using Application.Interfaces;
using Application.Interfaces.Repositories;
using Contracts.Models;
using Contracts.Responses;
using Domain;
using Infrastructure.Caching;
using ZiggyCreatures.Caching.Fusion;

namespace Infrastructure.Services;

// Cached decorator for EvaluationService using FusionCache, options are set to:
// Local: 5 minutes (evaluations are hot path but need frequent updates)
// Distributed: 30 minutes
// Cache key includes flag version for automatic invalidation
// Background operations enabled
public sealed class CachedEvaluationService(
    IEvaluationService innerService,
    IFusionCache cache,
    IFeatureFlagRepository featureFlagRepository) : IEvaluationService
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
        // Fetch flag to get a version for cache key
        // FeatureFlagRepository is decorated with CachedFeatureFlagRepository, so this is a fast L1/L2 lookup
        var flag = await featureFlagRepository.GetByKeyAsync(projectId, featureFlagKey, cancellationToken);

        // If the feature flag doesn't exist, no caching needed, return immediately
        if (flag == null)
            return await innerService.EvaluateAsync(projectId, featureFlagKey, context, cancellationToken);

        var contextHash = CacheKeys.HashContext(context.Groups);
        var cacheKey = CacheKeys.Evaluation(projectId, featureFlagKey, context.UserId, flag.Version, contextHash);

        return await cache.GetOrSetAsync(
            cacheKey,
            // Pass the already-fetched flag to avoid double DB lookup
            async ct => await innerService.EvaluateAsync(flag, context, ct),
            CacheOptions,
            cancellationToken);
    }

    public Task<EvaluationResponse> EvaluateAsync(FeatureFlag featureFlag, EvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        // Method to avoid calling the flag twice because of decoration
        return innerService.EvaluateAsync(featureFlag, context, cancellationToken);
    }
}