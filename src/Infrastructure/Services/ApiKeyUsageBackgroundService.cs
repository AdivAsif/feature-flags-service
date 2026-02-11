using Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class ApiKeyUsageBackgroundService : BackgroundService
{
    private static readonly TimeSpan MinUpdateInterval = TimeSpan.FromSeconds(30);

    private readonly Dictionary<Guid, DateTimeOffset> _lastUpdatedAtUtc = new();
    private readonly ILogger<ApiKeyUsageBackgroundService> _logger;

    private readonly ApiKeyUsageQueue _queue;
    private readonly IServiceProvider _serviceProvider;

    public ApiKeyUsageBackgroundService(
        ApiKeyUsageQueue queue,
        IServiceProvider serviceProvider,
        ILogger<ApiKeyUsageBackgroundService> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("API Key Usage Background Service started");

        var channel = _queue.GetChannel();
        await foreach (var apiKeyId in channel.Reader.ReadAllAsync(stoppingToken))
            try
            {
                var nowUtc = DateTimeOffset.UtcNow;
                if (_lastUpdatedAtUtc.TryGetValue(apiKeyId, out var lastUpdatedAtUtc) &&
                    nowUtc - lastUpdatedAtUtc < MinUpdateInterval)
                    continue;

                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();
                await repository.UpdateLastUsedAtAsync(apiKeyId, stoppingToken);
                _lastUpdatedAtUtc[apiKeyId] = nowUtc;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating LastUsedAt for ApiKeyId: {ApiKeyId}", apiKeyId);
            }

        _logger.LogInformation("API Key Usage Background Service stopped");
    }
}