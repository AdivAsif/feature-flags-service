using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SharedKernel;

namespace Application.Tests;

public class FeatureFlagsServiceTests
{
    private readonly AuditLogQueue _auditLogQueue;
    private readonly IKeyedRepository<FeatureFlag> _repository;
    private readonly IFeatureFlagsService _service;
    private readonly Guid _testProjectId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public FeatureFlagsServiceTests()
    {
        _repository = Substitute.For<IKeyedRepository<FeatureFlag>>();
        var logger = Substitute.For<ILogger<AuditLogQueue>>();
        _auditLogQueue = new AuditLogQueue(logger);
        var mapper = new FeatureFlagMapper();
        _service = new FeatureFlagsService(_repository, mapper, _auditLogQueue);
    }

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllFeatureFlags()
    {
        // Arrange
        var featureFlags = new List<FeatureFlag>
        {
            new()
            {
                Id = Guid.NewGuid(), ProjectId = _testProjectId, Key = "feature-1", Enabled = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(), ProjectId = _testProjectId, Key = "feature-2", Enabled = false,
                CreatedAt = DateTimeOffset.UtcNow
            }
        };
        _repository.GetAllAsync(Arg.Any<int?>(), Arg.Any<int?>()).Returns(featureFlags);

        // Act
        var result = await _service.GetAllAsync(_testProjectId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(f => f.Key == "feature-1");
        result.Should().Contain(f => f.Key == "feature-2");
    }

    #endregion

    #region GetPagedAsync Tests

    [Fact]
    public async Task GetPagedAsync_ShouldReturnPagedResults()
    {
        // Arrange
        var featureFlags = new List<FeatureFlag>
        {
            new()
            {
                Id = Guid.NewGuid(), ProjectId = _testProjectId, Key = "feature-1", Enabled = true,
                CreatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(), ProjectId = _testProjectId, Key = "feature-2", Enabled = false,
                CreatedAt = DateTimeOffset.UtcNow
            }
        };
        var pagedResult = new PagedResult<FeatureFlag>
        {
            Items = featureFlags,
            PageInfo = new PageInfo
            {
                HasNextPage = false,
                HasPreviousPage = false,
                TotalCount = 2,
                StartCursor = "cursor1",
                EndCursor = "cursor2"
            }
        };
        _repository.GetPagedAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>()).Returns(pagedResult);

        // Act
        var result = await _service.GetPagedAsync(_testProjectId);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.PageInfo.TotalCount.Should().Be(2);
        result.PageInfo.HasNextPage.Should().BeFalse();
    }

    #endregion

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WithValidId_ShouldReturnFeatureFlag()
    {
        // Arrange
        var id = Guid.NewGuid();
        var featureFlag = new FeatureFlag
        {
            Id = id,
            ProjectId = _testProjectId,
            Key = "test-feature",
            Description = "Test",
            Enabled = true,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _repository.GetByIdAsync(id).Returns(featureFlag);

        // Act
        var result = await _service.GetAsync(_testProjectId, id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.Key.Should().Be("test-feature");
        await _repository.Received(1).GetByIdAsync(id);
    }

    [Fact]
    public async Task GetAsync_WithInvalidId_ShouldThrowNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id).Returns((FeatureFlag?)null);

        // Act
        var act = async () => await _service.GetAsync(_testProjectId, id);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Feature Flag with id: {id} not found in project: {_testProjectId}");
    }

    #endregion

    #region GetByKeyAsync Tests

    [Fact]
    public async Task GetByKeyAsync_WithValidKey_ShouldReturnFeatureFlag()
    {
        // Arrange
        const string key = "test-feature";
        var featureFlag = new FeatureFlag
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Key = key,
            Description = "Test",
            Enabled = true,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _repository.GetByKeyAsync(_testProjectId, key).Returns(featureFlag);

        // Act
        var result = await _service.GetByKeyAsync(_testProjectId, key);

        // Assert
        result.Should().NotBeNull();
        result!.Key.Should().Be(key);
        await _repository.Received(1).GetByKeyAsync(_testProjectId, key);
    }

    [Fact]
    public async Task GetByKeyAsync_WithInvalidKey_ShouldThrowNotFoundException()
    {
        // Arrange
        var key = "non-existent";
        _repository.GetByKeyAsync(_testProjectId, key).Returns((FeatureFlag?)null);

        // Act
        var act = async () => await _service.GetByKeyAsync(_testProjectId, key);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Feature Flag with key: {key} not found in project: {_testProjectId}");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCreateFeatureFlag()
    {
        // Arrange
        var dto = new FeatureFlagDTO
        {
            Key = "new-feature",
            Description = "New feature",
            Enabled = true,
            Parameters = Array.Empty<FeatureFlagParameters>()
        };
        _repository.GetByKeyAsync(_testProjectId, dto.Key).Returns((FeatureFlag?)null);
        _repository.CreateAsync(Arg.Any<FeatureFlag>()).Returns(call =>
        {
            var flag = call.Arg<FeatureFlag>();
            flag.Id = Guid.NewGuid();
            flag.ProjectId = _testProjectId;
            flag.CreatedAt = DateTimeOffset.UtcNow;
            return flag;
        });

        // Act
        var result = await _service.CreateAsync(_testProjectId, dto);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be("new-feature");
        result.Description.Should().Be("New feature");
        await _repository.Received(1).CreateAsync(Arg.Any<FeatureFlag>());
    }

    [Fact]
    public async Task CreateAsync_WithNullKey_ShouldThrowBadRequestException()
    {
        // Arrange
        var dto = new FeatureFlagDTO { Key = null! };

        // Act
        var act = async () => await _service.CreateAsync(_testProjectId, dto);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Feature Flag key is required");
    }

    [Fact]
    public async Task CreateAsync_WithExistingKey_ShouldThrowBadRequestException()
    {
        // Arrange
        var dto = new FeatureFlagDTO { Key = "existing-feature" };
        var existingFlag = new FeatureFlag { Key = "existing-feature", ProjectId = _testProjectId };
        _repository.GetByKeyAsync(_testProjectId, dto.Key).Returns(existingFlag);

        // Act
        var act = async () => await _service.CreateAsync(_testProjectId, dto);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage($"Feature Flag with key: {dto.Key} already exists in project: {_testProjectId}");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithValidData_ShouldUpdateFeatureFlag()
    {
        // Arrange
        var key = "test-feature";
        var existingFlag = new FeatureFlag
        {
            Id = Guid.NewGuid(),
            ProjectId = _testProjectId,
            Key = key,
            Description = "Old description",
            Enabled = false,
            Version = 1,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var dto = new FeatureFlagDTO
        {
            Key = key,
            Description = "Updated description",
            Enabled = true,
            Parameters = Array.Empty<FeatureFlagParameters>()
        };
        _repository.GetByKeyAsync(_testProjectId, key).Returns(existingFlag);
        _repository.UpdateAsync(Arg.Any<FeatureFlag>()).Returns(call => call.Arg<FeatureFlag>());

        // Act
        var result = await _service.UpdateAsync(_testProjectId, key, dto);

        // Assert
        result.Should().NotBeNull();
        result.Description.Should().Be("Updated description");
        result.Enabled.Should().BeTrue();
        result.Version.Should().Be(2); // Version incremented
        await _repository.Received(1).UpdateAsync(Arg.Is<FeatureFlag>(f => f.Version == 2));
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyKey_ShouldThrowBadRequestException()
    {
        // Arrange
        var dto = new FeatureFlagDTO();

        // Act
        var act = async () => await _service.UpdateAsync(_testProjectId, "", dto);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Feature Flag key is required");
    }

    [Fact]
    public async Task UpdateAsync_WithNonExistentKey_ShouldThrowNotFoundException()
    {
        // Arrange
        var key = "non-existent";
        var dto = new FeatureFlagDTO { Key = key };
        _repository.GetByKeyAsync(_testProjectId, key).Returns((FeatureFlag?)null);

        // Act
        var act = async () => await _service.UpdateAsync(_testProjectId, key, dto);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_WithDifferentKey_ShouldThrowBadRequestException()
    {
        // Arrange
        var existingKey = "original-key";
        var newKey = "different-key";
        var existingFlag = new FeatureFlag { Key = existingKey, ProjectId = _testProjectId };
        var dto = new FeatureFlagDTO { Key = newKey };
        _repository.GetByKeyAsync(_testProjectId, existingKey).Returns(existingFlag);

        // Act
        var act = async () => await _service.UpdateAsync(_testProjectId, existingKey, dto);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Changing a Feature Flag key is not allowed*");
    }

    #endregion

    #region DeleteByKeyAsync Tests

    [Fact]
    public async Task DeleteByKeyAsync_WithValidKey_ShouldDeleteFeatureFlag()
    {
        // Arrange
        var key = "test-feature";
        var id = Guid.NewGuid();
        var featureFlag = new FeatureFlag { Id = id, Key = key, ProjectId = _testProjectId };
        _repository.GetByKeyAsync(_testProjectId, key).Returns(featureFlag);

        // Act
        await _service.DeleteByKeyAsync(_testProjectId, key);

        // Assert
        await _repository.Received(1).DeleteAsync(id);
    }

    [Fact]
    public async Task DeleteByKeyAsync_WithNonExistentKey_ShouldThrowNotFoundException()
    {
        // Arrange
        var key = "non-existent";
        _repository.GetByKeyAsync(_testProjectId, key).Returns((FeatureFlag?)null);

        // Act
        var act = async () => await _service.DeleteByKeyAsync(_testProjectId, key);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Feature Flag with key: {key} not found in project: {_testProjectId}");
    }

    #endregion
}