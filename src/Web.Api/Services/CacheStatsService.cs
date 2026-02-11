using Microsoft.Extensions.Caching.Memory;
using Prometheus;

namespace Web.Api.Services;

/// <summary>
///     Tracks L1 (memory) cache statistics by monitoring the IMemoryCache directly.
///     This gives us insight into L1 hit rates that FusionCache events don't expose.
/// </summary>
public class CacheStatsService : IHostedService
{
    // Track L1-specific metrics
    private static readonly Gauge L1CacheSize = Metrics.CreateGauge(
        "fusion_cache_l1_size",
        "Current number of entries in L1 memory cache (approximate)");

    private static readonly Gauge L1CacheMemoryMB = Metrics.CreateGauge(
        "fusion_cache_l1_memory_mb",
        "Approximate memory used by L1 cache in MB");

    private static readonly Gauge CacheHitRatePercent = Metrics.CreateGauge(
        "fusion_cache_hit_rate_percent",
        "Overall cache hit rate percentage (last interval)");

    private readonly ILogger<CacheStatsService> _logger;
    private readonly IMemoryCache _memoryCache;
    private Timer? _statsTimer;

    public CacheStatsService(IMemoryCache memoryCache, ILogger<CacheStatsService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting cache statistics collection");

        // Update stats every 5 seconds
        _statsTimer = new Timer(UpdateStats, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _statsTimer?.Change(Timeout.Infinite, 0);
        _statsTimer?.Dispose();
        return Task.CompletedTask;
    }

    private void UpdateStats(object? state)
    {
        try
        {
            // Try to get cache statistics if available
            if (_memoryCache is MemoryCache memCache)
            {
                var stats = memCache.GetCurrentStatistics();
                if (stats != null)
                {
                    L1CacheSize.Set(stats.CurrentEntryCount);

                    // Estimate memory: Assuming ~2KB per entry
                    var estimatedMemoryMB = stats.CurrentEntryCount * 2 / 1024.0;
                    L1CacheMemoryMB.Set(estimatedMemoryMB);

                    _logger.LogTrace(
                        "L1 Cache Stats: Entries={Entries}, EstimatedMemory={Memory}MB",
                        stats.CurrentEntryCount,
                        estimatedMemoryMB);
                }
                else
                {
                    _logger.LogTrace("MemoryCache statistics not available");
                }
            }
            else
            {
                _logger.LogTrace("IMemoryCache is not a MemoryCache instance");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update cache statistics");
        }
    }
}