using Application.Services;
using Contracts.Responses;
using Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Application.Tests;

public class AuditLogQueueTests
{
    private readonly AuditLogQueue _queue;

    public AuditLogQueueTests()
    {
        var logger = Substitute.For<ILogger<AuditLogQueue>>();
        _queue = new AuditLogQueue(logger);
    }

    [Fact]
    public async Task QueueAuditLogAsync_ShouldSuccessfullyQueueAuditLog()
    {
        // Arrange
        var auditLog = new AuditLogResponse
        {
            FeatureFlagId = Guid.NewGuid(),
            Action = AuditLogAction.Create,
            NewStateJson = "{\"enabled\":true}",
            CreatedAt = DateTimeOffset.UtcNow,
            PerformedByUserId = "user123",
            PerformedByUserEmail = "user@example.com"
        };

        // Act
        await _queue.QueueAuditLogAsync(auditLog);

        // Assert
        var channel = _queue.GetChannel();
        var success = channel.Reader.TryRead(out var queuedLog);
        success.Should().BeTrue();
        queuedLog.Should().NotBeNull();
        queuedLog!.FeatureFlagId.Should().Be(auditLog.FeatureFlagId);
        queuedLog.Action.Should().Be(auditLog.Action);
    }

    [Fact]
    public async Task QueueAuditLogAsync_ShouldQueueMultipleAuditLogs()
    {
        // Arrange
        var auditLog1 = new AuditLogResponse
        {
            FeatureFlagId = Guid.NewGuid(),
            Action = AuditLogAction.Create,
            NewStateJson = "{\"enabled\":true}",
            CreatedAt = DateTimeOffset.UtcNow,
            PerformedByUserId = "user1",
            PerformedByUserEmail = "user1@example.com"
        };
        var auditLog2 = new AuditLogResponse
        {
            FeatureFlagId = Guid.NewGuid(),
            Action = AuditLogAction.Update,
            PreviousStateJson = "{\"enabled\":true}",
            NewStateJson = "{\"enabled\":false}",
            CreatedAt = DateTimeOffset.UtcNow,
            PerformedByUserId = "user2",
            PerformedByUserEmail = "user2@example.com"
        };

        // Act
        await _queue.QueueAuditLogAsync(auditLog1);
        await _queue.QueueAuditLogAsync(auditLog2);

        // Assert
        var channel = _queue.GetChannel();

        var success1 = channel.Reader.TryRead(out var queuedLog1);
        success1.Should().BeTrue();
        queuedLog1!.FeatureFlagId.Should().Be(auditLog1.FeatureFlagId);

        var success2 = channel.Reader.TryRead(out var queuedLog2);
        success2.Should().BeTrue();
        queuedLog2!.FeatureFlagId.Should().Be(auditLog2.FeatureFlagId);
    }

    [Fact]
    public void GetChannel_ShouldReturnChannel()
    {
        // Act
        var channel = _queue.GetChannel();

        // Assert
        channel.Should().NotBeNull();
        channel.Reader.Should().NotBeNull();
        channel.Writer.Should().NotBeNull();
    }
}