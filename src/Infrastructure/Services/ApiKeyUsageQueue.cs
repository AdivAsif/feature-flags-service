using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Infrastructure.Services;

// Simple in-memory queue to track API key usage updates. This allows us to batch updates to the database and avoid
// excessive writes when an API key is used frequently within a short period of time. It is not critical to update the 
// LastUsedAt timestamp, it being eventually consistent is good enough for this use case
public sealed class ApiKeyUsageQueue
{
    private static readonly TimeSpan MinUpdateInterval = TimeSpan.FromHours(1);

    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastQueuedAtUtc = new();
    public ChannelReader<Guid> Reader => _channel.Reader;

    public void TryQueue(Guid apiKeyId)
    {
        var now = DateTimeOffset.UtcNow;

        if (_lastQueuedAtUtc.TryGetValue(apiKeyId, out var last) &&
            now - last < MinUpdateInterval)
            return;

        _lastQueuedAtUtc[apiKeyId] = now;
        _channel.Writer.TryWrite(apiKeyId);
    }

    public void CleanupOldEntries()
    {
        var cutoff = DateTimeOffset.UtcNow - MinUpdateInterval * 2;

        foreach (var kvp in _lastQueuedAtUtc)
            if (kvp.Value < cutoff)
                _lastQueuedAtUtc.TryRemove(kvp.Key, out _);
    }
}