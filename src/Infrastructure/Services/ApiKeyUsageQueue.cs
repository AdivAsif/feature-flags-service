using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class ApiKeyUsageQueue(ILogger<ApiKeyUsageQueue> logger)
{
    private static readonly TimeSpan DedupWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MinUpdateInterval = TimeSpan.FromHours(1);

    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly ConcurrentDictionary<Guid, byte> _recent = new();
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
    
    // public Channel<Guid> GetChannel()
    // {
    //     return _channel;
    // }
    //
    // /// <summary>
    // ///     Called from the auth handler for now. Must stay extremely cheap.
    // /// </summary>
    // public ValueTask QueueApiKeyUsageAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    // {
    //     // Fast-path: already queued recently
    //     if (!_recent.TryAdd(apiKeyId, 0))
    //         return ValueTask.CompletedTask;
    //
    //     // Unbounded channel: TryWrite is effectively infallible
    //     if (!_channel.Writer.TryWrite(apiKeyId))
    //         logger.LogWarning(
    //             "Failed to enqueue API key usage for ApiKeyId: {ApiKeyId}",
    //             apiKeyId);
    //
    //     // Schedule removal from de-dupe window
    //     _ = Task.Run(async () =>
    //     {
    //         await Task.Delay(DedupWindow, cancellationToken).ConfigureAwait(false);
    //         _recent.TryRemove(apiKeyId, out _);
    //     }, cancellationToken);
    //
    //     return ValueTask.CompletedTask;
    // }
}