using System.Threading.Channels;
using Contracts.Responses;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class AuditLogQueue(ILogger<AuditLogQueue> logger)
{
    private readonly Channel<AuditLogResponse> _channel = Channel.CreateUnbounded<AuditLogResponse>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public Channel<AuditLogResponse> GetChannel()
    {
        return _channel;
    }

    public async ValueTask QueueAuditLogAsync(AuditLogResponse auditLog, CancellationToken cancellationToken = default)
    {
        if (await _channel.Writer.WaitToWriteAsync(cancellationToken))
        {
            _channel.Writer.TryWrite(auditLog);
            logger.LogDebug("Audit log queued for FeatureFlagId: {FeatureFlagId}", auditLog.FeatureFlagId);
        }
        else
        {
            logger.LogWarning("Failed to queue audit log for FeatureFlagId: {FeatureFlagId}", auditLog.FeatureFlagId);
        }
    }
}