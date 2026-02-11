using Domain;
using FluentAssertions;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests;

public class FeatureFlagsRepositoryTests : IDisposable
{
    private readonly FeatureFlagsDbContext _context;
    private readonly FeatureFlagsRepository _repository;

    public FeatureFlagsRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<FeatureFlagsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new FeatureFlagsDbContext(options);
        _repository = new FeatureFlagsRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldAddFeatureFlagToDatabase()
    {
        // Arrange
        var featureFlag = new FeatureFlag
        {
            Id = Guid.NewGuid(),
            Key = "new-feature",
            Description = "New feature",
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _repository.CreateAsync(featureFlag);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be("new-feature");

        var dbFlag = await _context.FeatureFlags.FindAsync(featureFlag.Id);
        dbFlag.Should().NotBeNull();
        dbFlag!.Key.Should().Be("new-feature");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldModifyExistingFeatureFlag()
    {
        // Arrange
        var featureFlag = new FeatureFlag
        {
            Id = Guid.NewGuid(),
            Key = "test-feature",
            Description = "Original",
            Enabled = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _context.FeatureFlags.AddAsync(featureFlag);
        await _context.SaveChangesAsync();
        _context.Entry(featureFlag).State = EntityState.Detached;

        // Modify
        featureFlag.Description = "Updated";
        featureFlag.Enabled = true;

        // Act
        var result = await _repository.UpdateAsync(featureFlag);

        // Assert
        result.Description.Should().Be("Updated");
        result.Enabled.Should().BeTrue();

        var dbFlag = await _context.FeatureFlags.FindAsync(featureFlag.Id);
        dbFlag!.Description.Should().Be("Updated");
        dbFlag.Enabled.Should().BeTrue();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ShouldReturnFeatureFlag()
    {
        // Arrange
        var featureFlag = new FeatureFlag
        {
            Id = Guid.NewGuid(),
            Key = "test-feature",
            Description = "Test",
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _context.FeatureFlags.AddAsync(featureFlag);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(featureFlag.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(featureFlag.Id);
        result.Key.Should().Be("test-feature");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByKeyAsync Tests

    [Fact]
    public async Task GetByKeyAsync_WithExistingKey_ShouldReturnFeatureFlag()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var featureFlag = new FeatureFlag
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Key = "test-feature",
            Description = "Test",
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _context.FeatureFlags.AddAsync(featureFlag);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByKeyAsync(projectId, "test-feature");

        // Assert
        result.Should().NotBeNull();
        result!.Key.Should().Be("test-feature");
    }

    [Fact]
    public async Task GetByKeyAsync_WithNonExistentKey_ShouldReturnNull()
    {
        // Arrange
        var projectId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByKeyAsync(projectId, "non-existent");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllFeatureFlags()
    {
        // Arrange
        var flags = new[]
        {
            new FeatureFlag
                { Id = Guid.NewGuid(), Key = "feature-1", Enabled = true, CreatedAt = DateTimeOffset.UtcNow },
            new FeatureFlag
                { Id = Guid.NewGuid(), Key = "feature-2", Enabled = false, CreatedAt = DateTimeOffset.UtcNow },
            new FeatureFlag
                { Id = Guid.NewGuid(), Key = "feature-3", Enabled = true, CreatedAt = DateTimeOffset.UtcNow }
        };
        await _context.FeatureFlags.AddRangeAsync(flags);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync(null, null);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAllAsync_WithEmptyDatabase_ShouldReturnEmptyList()
    {
        // Act
        var result = await _repository.GetAllAsync(null, null);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetPagedAsync Tests

    [Fact]
    public async Task GetPagedAsync_FirstPage_ShouldReturnCorrectResults()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow;
        var flags = new[]
        {
            new FeatureFlag
                { Id = Guid.NewGuid(), Key = "feature-1", Enabled = true, CreatedAt = baseTime.AddMinutes(-3) },
            new FeatureFlag
                { Id = Guid.NewGuid(), Key = "feature-2", Enabled = false, CreatedAt = baseTime.AddMinutes(-2) },
            new FeatureFlag
                { Id = Guid.NewGuid(), Key = "feature-3", Enabled = true, CreatedAt = baseTime.AddMinutes(-1) }
        };
        await _context.FeatureFlags.AddRangeAsync(flags);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetPagedAsync(2);

        // Assert
        result.Items.Should().HaveCount(2);
        result.PageInfo.HasNextPage.Should().BeTrue();
        result.PageInfo.HasPreviousPage.Should().BeFalse();
        result.PageInfo.TotalCount.Should().Be(3);
        result.PageInfo.StartCursor.Should().NotBeNullOrEmpty();
        result.PageInfo.EndCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_WithAfterCursor_ShouldReturnNextPage()
    {
        // Arrange
        var baseTime = DateTimeOffset.UtcNow;
        var flags = new[]
        {
            new FeatureFlag
                { Id = Guid.NewGuid(), Key = "feature-1", Enabled = true, CreatedAt = baseTime.AddMinutes(-3) },
            new FeatureFlag
                { Id = Guid.NewGuid(), Key = "feature-2", Enabled = false, CreatedAt = baseTime.AddMinutes(-2) },
            new FeatureFlag
                { Id = Guid.NewGuid(), Key = "feature-3", Enabled = true, CreatedAt = baseTime.AddMinutes(-1) }
        };
        await _context.FeatureFlags.AddRangeAsync(flags);
        await _context.SaveChangesAsync();

        var firstPage = await _repository.GetPagedAsync(1);

        // Act
        var result = await _repository.GetPagedAsync(2, firstPage.PageInfo.EndCursor);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.First().Key.Should().Be("feature-2");
    }

    [Fact]
    public async Task GetPagedAsync_WithInvalidCursor_ShouldReturnFirstPage()
    {
        // Arrange
        var flags = new[]
        {
            new FeatureFlag
                { Id = Guid.NewGuid(), Key = "feature-1", Enabled = true, CreatedAt = DateTimeOffset.UtcNow }
        };
        await _context.FeatureFlags.AddRangeAsync(flags);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetPagedAsync(10, "invalid-cursor");

        // Assert
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldClampPageSize()
    {
        // Arrange
        var flags = Enumerable.Range(1, 10)
            .Select(i => new FeatureFlag
            {
                Id = Guid.NewGuid(),
                Key = $"feature-{i}",
                Enabled = true,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
            })
            .ToArray();
        await _context.FeatureFlags.AddRangeAsync(flags);
        await _context.SaveChangesAsync();

        // Act - request 200 items (should be clamped to 100)
        var result = await _repository.GetPagedAsync(200);

        // Assert
        result.Items.Should().HaveCount(10); // All items since we only have 10
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldRemoveFeatureFlag()
    {
        // Arrange
        var featureFlag = new FeatureFlag
        {
            Id = Guid.NewGuid(),
            Key = "test-feature",
            Description = "Test",
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _context.FeatureFlags.AddAsync(featureFlag);
        await _context.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(featureFlag.Id);

        // Assert
        var dbFlag = await _context.FeatureFlags.FindAsync(featureFlag.Id);
        dbFlag.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ShouldNotThrow()
    {
        // Act & Assert
        var act = async () => await _repository.DeleteAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    #endregion
}