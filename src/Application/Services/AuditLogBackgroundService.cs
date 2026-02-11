using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class AuditLogBackgroundService : BackgroundService
{
    private readonly ILogger<AuditLogBackgroundService> _logger;
    private readonly AuditLogQueue _queue;
    private readonly IServiceProvider _serviceProvider;

    public AuditLogBackgroundService(
        AuditLogQueue queue,
        IServiceProvider serviceProvider,
        ILogger<AuditLogBackgroundService> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit Log Background Service started");

        var channel = _queue.GetChannel();
        await foreach (var auditLog in channel.Reader.ReadAllAsync(stoppingToken))
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var auditLogsService = scope.ServiceProvider.GetRequiredService<IAuditLogsService>();
                await auditLogsService.AppendAsync(auditLog);
                _logger.LogDebug("Processed audit log for FeatureFlagId: {FeatureFlagId}", auditLog.FeatureFlagId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audit log for FeatureFlagId: {FeatureFlagId}",
                    auditLog.FeatureFlagId);
            }

        _logger.LogInformation("Audit Log Background Service stopped");
    }
}