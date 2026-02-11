using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class AuditLogBackgroundService(
    AuditLogQueue queue,
    IServiceProvider serviceProvider,
    ILogger<AuditLogBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = queue.GetChannel();
        await foreach (var auditLog in channel.Reader.ReadAllAsync(stoppingToken))
            try
            {
                using var scope = serviceProvider.CreateScope();
                var auditLogsService = scope.ServiceProvider.GetRequiredService<IAuditLogsService>();
                await auditLogsService.AppendAsync(auditLog, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing audit log for FeatureFlagId: {FeatureFlagId}",
                    auditLog.FeatureFlagId);
            }
    }
}