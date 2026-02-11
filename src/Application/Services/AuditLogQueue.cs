using System.Threading.Channels;
using Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class AuditLogQueue(ILogger<AuditLogQueue> logger)
{
    private readonly Channel<AuditLogDto> _channel = Channel.CreateUnbounded<AuditLogDto>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public Channel<AuditLogDto> GetChannel()
    {
        return _channel;
    }

    public async ValueTask QueueAuditLogAsync(AuditLogDto auditLog, CancellationToken cancellationToken = default)
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