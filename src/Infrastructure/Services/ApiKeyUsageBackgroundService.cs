using Application.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class ApiKeyUsageBackgroundService(
    ApiKeyUsageQueue queue,
    IServiceProvider serviceProvider,
    ILogger<ApiKeyUsageBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var apiKeyId in queue.Reader.ReadAllAsync(stoppingToken))
            try
            {
                using var scope = serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();
                await repository.UpdateLastUsedAtAsync(apiKeyId, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Error updating LastUsedAt for ApiKeyId: {ApiKeyId}", apiKeyId);
            }
    }
}