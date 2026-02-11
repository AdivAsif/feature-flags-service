using System.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Prometheus;

namespace Web.Api.Services;

public sealed class MetricsDistributedCache : IDistributedCache
{
    private static readonly Histogram RedisLatency =
        Metrics.CreateHistogram(
            "fusion_cache_redis_latency_ms",
            "Latency of Redis operations via IDistributedCache",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.1, 2, 12) // 0.1ms → ~200ms
            });

    private readonly IDistributedCache _inner;

    public MetricsDistributedCache(IDistributedCache inner)
    {
        _inner = inner;
    }

    public byte[]? Get(string key)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return _inner.Get(key);
        }
        finally
        {
            sw.Stop();
            RedisLatency.Observe(sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await _inner.GetAsync(key, token);
        }
        finally
        {
            sw.Stop();
            RedisLatency.Observe(sw.Elapsed.TotalMilliseconds);
        }
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        _inner.Set(key, value, options);
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        return _inner.SetAsync(key, value, options, token);
    }

    public void Refresh(string key)
    {
        _inner.Refresh(key);
    }

    public Task RefreshAsync(string key, CancellationToken token = default)
    {
        return _inner.RefreshAsync(key, token);
    }

    public void Remove(string key)
    {
        _inner.Remove(key);
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        return _inner.RemoveAsync(key, token);
    }
}