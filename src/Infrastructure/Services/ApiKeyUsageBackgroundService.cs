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
    private static readonly TimeSpan MinUpdateInterval = TimeSpan.FromHours(1);

    private readonly Dictionary<Guid, DateTimeOffset> _lastUpdatedAtUtc = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogDebug("API Key Usage Background Service started");

        var channel = queue.GetChannel();
        await foreach (var apiKeyId in channel.Reader.ReadAllAsync(stoppingToken))
            try
            {
                var nowUtc = DateTimeOffset.UtcNow;
                if (_lastUpdatedAtUtc.TryGetValue(apiKeyId, out var lastUpdatedAtUtc) &&
                    nowUtc - lastUpdatedAtUtc < MinUpdateInterval)
                    continue;

                using var scope = serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();
                await repository.UpdateLastUsedAtAsync(apiKeyId, stoppingToken);
                _lastUpdatedAtUtc[apiKeyId] = nowUtc;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating LastUsedAt for ApiKeyId: {ApiKeyId}", apiKeyId);
            }

        logger.LogDebug("API Key Usage Background Service stopped");
    }
}