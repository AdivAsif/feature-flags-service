using Prometheus;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Events;

namespace Web.Api.Services;

public class CacheMetricsService(ILogger<CacheMetricsService> logger)
{
    // Prometheus metrics with granular labels
    private static readonly Counter CacheHitsTotal = Metrics.CreateCounter(
        "fusion_cache_hits_total",
        "Total number of cache hits",
        new CounterConfiguration { LabelNames = ["level", "state"] });

    private static readonly Counter CacheMissesTotal = Metrics.CreateCounter(
        "fusion_cache_misses_total",
        "Total number of cache misses",
        new CounterConfiguration { LabelNames = ["level"] });

    private static readonly Counter CacheSetOperations = Metrics.CreateCounter(
        "fusion_cache_set_total",
        "Total number of cache set operations",
        new CounterConfiguration { LabelNames = ["level"] });

    private static readonly Counter CacheRemoveOperations = Metrics.CreateCounter(
        "fusion_cache_remove_total",
        "Total number of cache remove operations");

    private static readonly Histogram CacheOperationDuration = Metrics.CreateHistogram(
        "fusion_cache_operation_duration_seconds",
        "Duration of cache operations",
        new HistogramConfiguration
        {
            LabelNames = ["operation"],
            Buckets = Histogram.ExponentialBuckets(0.00001, 2, 15) // 10µs to 163ms
        });

    public void AttachToCache(IFusionCache cache)
    {
        // Hit/Miss events
        cache.Events.FailSafeActivate += OnFailSafeActivate;

        cache.Events.Memory.Hit += OnL1CacheHit;
        cache.Events.Memory.Miss += OnL1CacheMiss;

        cache.Events.Distributed.Hit += OnL2CacheHit;
        cache.Events.Distributed.Miss += OnL2CacheMiss;

        // Set/Remove events for tracking writes
        cache.Events.Set += OnCacheSet;
        cache.Events.Remove += OnCacheRemove;

        logger.LogDebug("Cache metrics monitoring enabled with enhanced tracking");
    }

    private void OnCacheSet(object? sender, FusionCacheEntryEventArgs e)
    {
        // Set happens on L1 and potentially L2
        // We can track frequency of cache updates
        CacheSetOperations.WithLabels("memory").Inc();

        logger.LogTrace("Cache SET: Key={Key}", e.Key);
    }

    private void OnCacheRemove(object? sender, FusionCacheEntryEventArgs e)
    {
        CacheRemoveOperations.Inc();

        logger.LogTrace("Cache REMOVE: Key={Key}", e.Key);
    }

    private void OnFailSafeActivate(object? sender, FusionCacheEntryEventArgs e)
    {
        logger.LogWarning("Cache FAIL-SAFE activated: Key={Key}", e.Key);
    }

    private void OnL1CacheHit(object? sender, FusionCacheEntryHitEventArgs e)
    {
        CacheHitsTotal
            .WithLabels("l1", e.IsStale ? "stale" : "fresh")
            .Inc();
        logger.LogTrace(
            "L1 Cache HIT: Key={Key}, State={State}",
            e.Key,
            e.IsStale ? "stale" : "fresh");
    }

    private void OnL1CacheMiss(object? sender, FusionCacheEntryEventArgs e)
    {
        CacheMissesTotal
            .WithLabels("l1")
            .Inc();
        logger.LogTrace("L1 Cache MISS: Key={Key}", e.Key);
    }

    private void OnL2CacheHit(object? sender, FusionCacheEntryHitEventArgs e)
    {
        CacheHitsTotal
            .WithLabels("l2", e.IsStale ? "stale" : "fresh")
            .Inc();
        logger.LogTrace(
            "L2 Cache HIT: Key={Key}, State={State}",
            e.Key,
            e.IsStale ? "stale" : "fresh");
    }

    private void OnL2CacheMiss(object? sender, FusionCacheEntryEventArgs e)
    {
        CacheMissesTotal
            .WithLabels("l2")
            .Inc();
        logger.LogTrace("L2 Cache MISS: Key={Key}", e.Key);
    }
}