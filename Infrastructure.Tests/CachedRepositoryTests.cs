using Domain;
using FluentAssertions;
using Infrastructure.Repositories;
using NSubstitute;
using SharedKernel;
using ZiggyCreatures.Caching.Fusion;

namespace Infrastructure.Tests;

public class CachedRepositoryTests
{
    private readonly IFusionCache _cache;
    private readonly CachedRepository<FeatureFlag> _cachedRepository;
    private readonly IRepository<FeatureFlag> _innerRepository;

    public CachedRepositoryTests()
    {
        _innerRepository = Substitute.For<IRepository<FeatureFlag>>();
        _cache = Substitute.For<IFusionCache>();
        _cachedRepository = new CachedRepository<FeatureFlag>(_innerRepository, _cache);
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ShouldUseCacheFirst()
    {
        // Arrange
        var id = Guid.NewGuid();
        var featureFlag = new FeatureFlag { Id = id, Key = "test" };

        _cache.GetOrSetAsync<FeatureFlag>(
                Arg.Any<string>(),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<FeatureFlag>, CancellationToken, Task<FeatureFlag?>>>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(featureFlag);

        // Act
        var result = await _cachedRepository.GetByIdAsync(id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
    }

    #endregion

    #region GetByKeyAsync Tests

    [Fact]
    public async Task GetByKeyAsync_ShouldUseMappingCache()
    {
        // Arrange
        var key = "test-feature";
        var id = Guid.NewGuid();
        var featureFlag = new FeatureFlag { Id = id, Key = key };

        _cache.GetOrSetAsync(
                Arg.Is<string>(s => s.Contains("mapping")),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<Guid?>, CancellationToken, Task<Guid?>>>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(id);

        _cache.GetOrSetAsync<FeatureFlag>(
                Arg.Is<string>(s => s.Contains(id.ToString())),
                Arg.Any<Func<FusionCacheFactoryExecutionContext<FeatureFlag>, CancellationToken, Task<FeatureFlag?>>>(),
                Arg.Any<FusionCacheEntryOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(featureFlag);

        // Act
        var result = await _cachedRepository.GetByKeyAsync(key);

        // Assert
        result.Should().NotBeNull();
        result!.Key.Should().Be(key);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldInvalidateAllCache()
    {
        // Arrange
        var featureFlag = new FeatureFlag { Id = Guid.NewGuid(), Key = "new-feature" };
        _innerRepository.CreateAsync(featureFlag).Returns(featureFlag);

        // Act
        await _cachedRepository.CreateAsync(featureFlag);

        // Assert
        await _innerRepository.Received(1).CreateAsync(featureFlag);
        await _cache.Received().SetAsync(
            Arg.Is<string>(s => s.Contains(featureFlag.Id.ToString())),
            featureFlag,
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
        await _cache.Received().RemoveAsync(
            Arg.Is<string>(s => s.Contains("all")),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldUpdateCacheAndInvalidateAll()
    {
        // Arrange
        var featureFlag = new FeatureFlag { Id = Guid.NewGuid(), Key = "test-feature" };
        _innerRepository.UpdateAsync(featureFlag).Returns(featureFlag);

        // Act
        await _cachedRepository.UpdateAsync(featureFlag);

        // Assert
        await _innerRepository.Received(1).UpdateAsync(featureFlag);
        await _cache.Received().SetAsync(
            Arg.Is<string>(s => s.Contains(featureFlag.Id.ToString())),
            featureFlag,
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldRemoveFromCacheAndInvalidateAll()
    {
        // Arrange
        var id = Guid.NewGuid();
        var featureFlag = new FeatureFlag { Id = id, Key = "test-feature" };
        _innerRepository.GetByIdAsync(id).Returns(featureFlag);

        // Act
        await _cachedRepository.DeleteAsync(id);

        // Assert
        await _innerRepository.Received(1).DeleteAsync(id);
        await _cache.Received().RemoveAsync(
            Arg.Is<string>(s => s.Contains(id.ToString()) || s.Contains("all") || s.Contains("mapping")),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    #endregion
}