using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class ApiKeyUsageQueue
{
    private readonly Channel<Guid> _channel;
    private readonly ILogger<ApiKeyUsageQueue> _logger;

    public ApiKeyUsageQueue(ILogger<ApiKeyUsageQueue> logger)
    {
        _logger = logger;
        _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public Channel<Guid> GetChannel()
    {
        return _channel;
    }

    public async ValueTask QueueApiKeyUsageAsync(Guid apiKeyId)
    {
        if (!await _channel.Writer.WaitToWriteAsync())
        {
            _logger.LogWarning("Failed to queue API key usage for ApiKeyId: {ApiKeyId}", apiKeyId);
            return;
        }

        _channel.Writer.TryWrite(apiKeyId);
    }
}