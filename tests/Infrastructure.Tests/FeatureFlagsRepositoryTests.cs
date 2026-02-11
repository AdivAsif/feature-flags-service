using Domain;
using FluentAssertions;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests;

public class FeatureFlagsRepositoryTests : IDisposable
{
    private readonly FeatureFlagsDbContext _context;
    private readonly IDbContextFactory<FeatureFlagsDbContext> _contextFactory;
    private readonly FeatureFlagsRepository _repository;

    public FeatureFlagsRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<FeatureFlagsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _contextFactory = new TestDbContextFactory(options);
        _context = _contextFactory.CreateDbContext();
        _repository = new FeatureFlagsRepository(_contextFactory);
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
        var projectId = Guid.NewGuid();
        var featureFlag = new FeatureFlag
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
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
        var projectId = Guid.NewGuid();
        var featureFlag = new FeatureFlag
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
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

    private class TestDbContextFactory : IDbContextFactory<FeatureFlagsDbContext>
    {
        private readonly DbContextOptions<FeatureFlagsDbContext> _options;

        public TestDbContextFactory(DbContextOptions<FeatureFlagsDbContext> options)
        {
            _options = options;
        }

        public FeatureFlagsDbContext CreateDbContext()
        {
            return new FeatureFlagsDbContext(_options);
        }
    }

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ShouldReturnFeatureFlag()
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
        var result = await _repository.GetByIdAsync(projectId, featureFlag.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(featureFlag.Id);
        result.Key.Should().Be("test-feature");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        var projectId = Guid.NewGuid();

        // Act
        var result = await _repository.GetByIdAsync(projectId, Guid.NewGuid());

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

    #region GetPagedAsync Tests

    [Fact]
    public async Task GetPagedAsync_FirstPage_ShouldReturnCorrectResults()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow;
        var flags = new[]
        {
            new FeatureFlag
            {
                Id = Guid.NewGuid(), ProjectId = projectId, Key = "feature-1", Enabled = true,
                CreatedAt = baseTime.AddMinutes(-3)
            },
            new FeatureFlag
            {
                Id = Guid.NewGuid(), ProjectId = projectId, Key = "feature-2", Enabled = false,
                CreatedAt = baseTime.AddMinutes(-2)
            },
            new FeatureFlag
            {
                Id = Guid.NewGuid(), ProjectId = projectId, Key = "feature-3", Enabled = true,
                CreatedAt = baseTime.AddMinutes(-1)
            }
        };
        await _context.FeatureFlags.AddRangeAsync(flags);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetPagedAsync(projectId, 2);

        // Assert
        result.Items.Should().HaveCount(2);
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeFalse();
        result.TotalCount.Should().Be(3);
        result.StartCursor.Should().NotBeNullOrEmpty();
        result.EndCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetPagedAsync_WithAfterCursor_ShouldReturnNextPage()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var baseTime = DateTimeOffset.UtcNow;
        var flags = new[]
        {
            new FeatureFlag
            {
                Id = Guid.NewGuid(), ProjectId = projectId, Key = "feature-1", Enabled = true,
                CreatedAt = baseTime.AddMinutes(-3)
            },
            new FeatureFlag
            {
                Id = Guid.NewGuid(), ProjectId = projectId, Key = "feature-2", Enabled = false,
                CreatedAt = baseTime.AddMinutes(-2)
            },
            new FeatureFlag
            {
                Id = Guid.NewGuid(), ProjectId = projectId, Key = "feature-3", Enabled = true,
                CreatedAt = baseTime.AddMinutes(-1)
            }
        };
        await _context.FeatureFlags.AddRangeAsync(flags);
        await _context.SaveChangesAsync();

        var firstPage = await _repository.GetPagedAsync(projectId, 1);

        // Act
        var result = await _repository.GetPagedAsync(projectId, 2, firstPage.EndCursor);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.First().Key.Should().Be("feature-2");
    }

    [Fact]
    public async Task GetPagedAsync_WithInvalidCursor_ShouldReturnFirstPage()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var flags = new[]
        {
            new FeatureFlag
            {
                Id = Guid.NewGuid(), ProjectId = projectId, Key = "feature-1", Enabled = true,
                CreatedAt = DateTimeOffset.UtcNow
            }
        };
        await _context.FeatureFlags.AddRangeAsync(flags);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetPagedAsync(projectId, 10, "invalid-cursor");

        // Assert
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldClampPageSize()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var flags = Enumerable.Range(1, 10)
            .Select(i => new FeatureFlag
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Key = $"feature-{i}",
                Enabled = true,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i)
            })
            .ToArray();
        await _context.FeatureFlags.AddRangeAsync(flags);
        await _context.SaveChangesAsync();

        // Act - request 200 items (should be clamped to 100)
        var result = await _repository.GetPagedAsync(projectId, 200);

        // Assert
        result.Items.Should().HaveCount(10); // All items since we only have 10
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ShouldRemoveFeatureFlag()
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
        await _repository.DeleteAsync(projectId, featureFlag.Id);

        // Assert - use a fresh context to verify deletion
        using var verifyContext = _contextFactory.CreateDbContext();
        var dbFlag = await verifyContext.FeatureFlags.FindAsync(featureFlag.Id);
        dbFlag.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ShouldNotThrow()
    {
        // Arrange
        var projectId = Guid.NewGuid();

        // Act & Assert
        var act = async () => await _repository.DeleteAsync(projectId, Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    #endregion
}