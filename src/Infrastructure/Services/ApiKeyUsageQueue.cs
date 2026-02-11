using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class ApiKeyUsageQueue(ILogger<ApiKeyUsageQueue> logger)
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public Channel<Guid> GetChannel()
    {
        return _channel;
    }

    public async ValueTask QueueApiKeyUsageAsync(Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        if (!await _channel.Writer.WaitToWriteAsync(cancellationToken))
        {
            logger.LogWarning("Failed to queue API key usage for ApiKeyId: {ApiKeyId}", apiKeyId);
            return;
        }

        _channel.Writer.TryWrite(apiKeyId);
    }
}