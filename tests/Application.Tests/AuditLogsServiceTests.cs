using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Services;
using Domain;
using FluentAssertions;
using NSubstitute;
using SharedKernel;

namespace Application.Tests;

public class AuditLogsServiceTests
{
    private readonly IRepository<AuditLog> _repository;
    private readonly IAuditLogsService _service;

    public AuditLogsServiceTests()
    {
        _repository = Substitute.For<IRepository<AuditLog>>();
        var mapper = new AuditLogMapper();
        _service = new AuditLogsService(_repository, mapper);
    }

    #region GetPagedAsync Tests

    [Fact]
    public async Task GetPagedAsync_ShouldReturnPagedResults()
    {
        // Arrange
        var featureFlagId = Guid.NewGuid();
        var auditLogs = new List<AuditLog>
        {
            new()
            {
                Id = Guid.NewGuid(),
                FeatureFlagId = featureFlagId,
                Action = AuditLogAction.Create,
                NewStateJson = "{\"enabled\":true}",
                CreatedAt = DateTime.UtcNow,
                PerformedByUserId = "user1",
                PerformedByUserEmail = "user1@example.com"
            }
        };
        var pagedResult = new PagedResult<AuditLog>
        {
            Items = auditLogs,
            PageInfo = new PageInfo
            {
                HasNextPage = false,
                HasPreviousPage = false,
                TotalCount = 1,
                StartCursor = "cursor1",
                EndCursor = "cursor1"
            }
        };
        _repository.GetPagedAsync().Returns(pagedResult);

        // Act
        var result = await _service.GetPagedAsync();

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.PageInfo.TotalCount.Should().Be(1);
        result.PageInfo.HasNextPage.Should().BeFalse();
    }

    #endregion

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WithValidId_ShouldReturnAuditLog()
    {
        // Arrange
        var id = Guid.NewGuid();
        var featureFlagId = Guid.NewGuid();
        var auditLog = new AuditLog
        {
            Id = id,
            FeatureFlagId = featureFlagId,
            Action = AuditLogAction.Create,
            NewStateJson = "{\"enabled\":true}",
            CreatedAt = DateTime.UtcNow,
            PerformedByUserId = "user123",
            PerformedByUserEmail = "user@example.com"
        };
        _repository.GetByIdAsync(id).Returns(auditLog);

        // Act
        var result = await _service.GetAsync(id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.FeatureFlagId.Should().Be(featureFlagId);
        result.Action.Should().Be(AuditLogAction.Create);
        await _repository.Received(1).GetByIdAsync(id);
    }

    [Fact]
    public async Task GetAsync_WithInvalidId_ShouldThrowNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id).Returns((AuditLog?)null);

        // Act
        var act = async () => await _service.GetAsync(id);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Audit Log with id: {id} not found");
    }

    #endregion

    #region AppendAsync Tests

    [Fact]
    public async Task AppendAsync_WithNewStateJson_ShouldCreateAuditLog()
    {
        // Arrange
        var featureFlagId = Guid.NewGuid();
        var dto = new AuditLogDto
        {
            FeatureFlagId = featureFlagId,
            Action = AuditLogAction.Create,
            NewStateJson = "{\"enabled\":true}",
            PerformedByUserId = "user123",
            PerformedByUserEmail = "user@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _repository.CreateAsync(Arg.Any<AuditLog>()).Returns(call =>
        {
            var log = call.Arg<AuditLog>();
            log.Id = Guid.NewGuid();
            return log;
        });

        // Act
        var result = await _service.AppendAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.FeatureFlagId.Should().Be(featureFlagId);
        result.Action.Should().Be(AuditLogAction.Create);
        await _repository.Received(1).CreateAsync(Arg.Any<AuditLog>());
    }

    [Fact]
    public async Task AppendAsync_WithPreviousStateJson_ShouldCreateAuditLog()
    {
        // Arrange
        var featureFlagId = Guid.NewGuid();
        var dto = new AuditLogDto
        {
            FeatureFlagId = featureFlagId,
            Action = AuditLogAction.Delete,
            PreviousStateJson = "{\"enabled\":true}",
            PerformedByUserId = "user123",
            PerformedByUserEmail = "user@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _repository.CreateAsync(Arg.Any<AuditLog>()).Returns(call =>
        {
            var log = call.Arg<AuditLog>();
            log.Id = Guid.NewGuid();
            return log;
        });

        // Act
        var result = await _service.AppendAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.FeatureFlagId.Should().Be(featureFlagId);
        result.Action.Should().Be(AuditLogAction.Delete);
        await _repository.Received(1).CreateAsync(Arg.Any<AuditLog>());
    }

    [Fact]
    public async Task AppendAsync_WithBothStateJsons_ShouldCreateAuditLog()
    {
        // Arrange
        var featureFlagId = Guid.NewGuid();
        var dto = new AuditLogDto
        {
            FeatureFlagId = featureFlagId,
            Action = AuditLogAction.Update,
            PreviousStateJson = "{\"enabled\":true}",
            NewStateJson = "{\"enabled\":false}",
            PerformedByUserId = "user123",
            PerformedByUserEmail = "user@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _repository.CreateAsync(Arg.Any<AuditLog>()).Returns(call =>
        {
            var log = call.Arg<AuditLog>();
            log.Id = Guid.NewGuid();
            return log;
        });

        // Act
        var result = await _service.AppendAsync(dto);

        // Assert
        result.Should().NotBeNull();
        result.FeatureFlagId.Should().Be(featureFlagId);
        result.Action.Should().Be(AuditLogAction.Update);
        result.NewStateJson.Should().Be("{\"enabled\":false}");
        result.PreviousStateJson.Should().Be("{\"enabled\":true}");
        await _repository.Received(1).CreateAsync(Arg.Any<AuditLog>());
    }

    [Fact]
    public async Task AppendAsync_WithNoStateJson_ShouldThrowBadRequestException()
    {
        // Arrange
        var dto = new AuditLogDto
        {
            FeatureFlagId = Guid.NewGuid(),
            Action = AuditLogAction.Create,
            PerformedByUserId = "user123",
            PerformedByUserEmail = "user@example.com"
        };

        // Act
        var act = async () => await _service.AppendAsync(dto);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("Either state JSON is required");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_WithValidId_ShouldDeleteAuditLog()
    {
        // Arrange
        var id = Guid.NewGuid();
        var auditLog = new AuditLog
        {
            Id = id,
            FeatureFlagId = Guid.NewGuid(),
            Action = AuditLogAction.Create,
            NewStateJson = "{\"enabled\":true}",
            CreatedAt = DateTime.UtcNow,
            PerformedByUserId = "user123",
            PerformedByUserEmail = "user@example.com"
        };
        _repository.GetByIdAsync(id).Returns(auditLog);

        // Act
        await _service.DeleteAsync(id);

        // Assert
        await _repository.Received(1).DeleteAsync(id);
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentId_ShouldThrowNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repository.GetByIdAsync(id).Returns((AuditLog?)null);

        // Act
        var act = async () => await _service.DeleteAsync(id);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Audit Log with id: {id} not found");
    }

    #endregion
}