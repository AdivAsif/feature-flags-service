using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class ApiKeyUsageCleanupService(
    ApiKeyUsageQueue queue,
    ILogger<ApiKeyUsageCleanupService> logger)
    : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug("API Key usage cleanup service started");

        using var timer = new PeriodicTimer(CleanupInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                queue.CleanupOldEntries();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during API key usage cleanup");
            }
        }
    }
}