using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

// Every 10 minutes, clean up API key usage entries to prevent unbounded growth of the queue
public sealed class ApiKeyUsageCleanupService(
    ApiKeyUsageQueue queue,
    ILogger<ApiKeyUsageCleanupService> logger)
    : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
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