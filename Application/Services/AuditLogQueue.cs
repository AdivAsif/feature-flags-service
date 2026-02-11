using System.Threading.Channels;
using Application.DTOs;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class AuditLogQueue
{
    private readonly Channel<AuditLogDTO> _channel;
    private readonly ILogger<AuditLogQueue> _logger;

    public AuditLogQueue(ILogger<AuditLogQueue> logger)
    {
        _logger = logger;
        _channel = Channel.CreateUnbounded<AuditLogDTO>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public Channel<AuditLogDTO> GetChannel() => _channel;

    public async ValueTask QueueAuditLogAsync(AuditLogDTO auditLog)
    {
        if (await _channel.Writer.WaitToWriteAsync())
        {
            _channel.Writer.TryWrite(auditLog);
            _logger.LogDebug("Audit log queued for FeatureFlagId: {FeatureFlagId}", auditLog.FeatureFlagId);
        }
        else
        {
            _logger.LogWarning("Failed to queue audit log for FeatureFlagId: {FeatureFlagId}", auditLog.FeatureFlagId);
        }
    }
}
