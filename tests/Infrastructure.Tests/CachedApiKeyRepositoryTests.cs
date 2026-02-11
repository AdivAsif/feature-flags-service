using Application.Interfaces.Repositories;
using Domain;
using FluentAssertions;
using Infrastructure.Repositories;
using Infrastructure.Services;
using NSubstitute;
using ZiggyCreatures.Caching.Fusion;

namespace Infrastructure.Tests;

public class CachedApiKeyRepositoryTests
{
    private readonly IFusionCache _cache;
    private readonly CachedApiKeyRepository _cachedApiKeyRepository;
    private readonly IApiKeyRepository _innerRepository;

    public CachedApiKeyRepositoryTests()
    {
        _innerRepository = Substitute.For<IApiKeyRepository>();
        _cache = Substitute.For<IFusionCache>();
        var usageQueue = new ApiKeyUsageQueue();
        _cachedApiKeyRepository = new CachedApiKeyRepository(_innerRepository, _cache, usageQueue);
    }

    [Fact]
    public async Task UpdateLastUsedAtAsync_ShouldQueueForBackgroundProcessing()
    {
        // Arrange
        var apiKeyId = Guid.NewGuid();

        // Act
        await _cachedApiKeyRepository.UpdateLastUsedAtAsync(apiKeyId);

        // Assert - Should NOT call inner repository directly (queued for background processing)
        await _innerRepository.DidNotReceive().UpdateLastUsedAtAsync(apiKeyId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RevokeAsync_ShouldInvalidateSecurityRelevantCacheEntries()
    {
        // Arrange
        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            KeyHash = "hash_123",
            IsActive = true,
            Name = "test",
            Scopes = "flags:read"
        };

        _innerRepository.GetByIdAsync(apiKey.Id, Arg.Any<CancellationToken>()).Returns(apiKey);

        // Act
        await _cachedApiKeyRepository.RevokeAsync(apiKey.Id);

        // Assert
        await _innerRepository.Received(1).GetByIdAsync(apiKey.Id, Arg.Any<CancellationToken>());
        await _innerRepository.Received(1).RevokeAsync(apiKey.Id, Arg.Any<CancellationToken>());

        await _cache.Received().RemoveAsync(
            Arg.Is<string>(k => k == $"apikey:{apiKey.KeyHash}"),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());

        await _cache.Received().RemoveAsync(
            Arg.Is<string>(k => k == $"apikey:project:{apiKey.ProjectId}"),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());

        apiKey.KeyHash.Should().NotBeNullOrWhiteSpace();
    }
}