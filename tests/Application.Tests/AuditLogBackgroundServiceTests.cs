using Application.Interfaces;
using Application.Services;
using Contracts.Responses;
using Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Application.Tests;

public class AuditLogBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldProcessQueuedAuditLogs()
    {
        // Arrange
        var logger = Substitute.For<ILogger<AuditLogBackgroundService>>();
        var queueLogger = Substitute.For<ILogger<AuditLogQueue>>();
        var auditLogsService = Substitute.For<IAuditLogsService>();
        var queue = new AuditLogQueue(queueLogger);

        var serviceProvider = Substitute.For<IServiceProvider>();
        var serviceScope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();

        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(serviceScope);
        serviceScope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IAuditLogsService)).Returns(auditLogsService);

        var backgroundService = new AuditLogBackgroundService(queue, serviceProvider, logger);

        var auditLog = new AuditLogResponse
        {
            FeatureFlagId = Guid.NewGuid(),
            Action = AuditLogAction.Create,
            NewStateJson = "{\"enabled\":true}",
            CreatedAt = DateTimeOffset.UtcNow,
            PerformedByUserId = "user123",
            PerformedByUserEmail = "user@example.com"
        };

        auditLogsService.AppendAsync(Arg.Any<AuditLogResponse>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(auditLog));

        // Act
        await queue.QueueAuditLogAsync(auditLog);

        var cts = new CancellationTokenSource();
        await backgroundService.StartAsync(cts.Token);

        // Give it a moment to process
        await Task.Delay(100, cts.Token);

        // Stop the service
        await cts.CancelAsync();
        await backgroundService.StopAsync(CancellationToken.None);

        // Assert
        await auditLogsService.Received(1).AppendAsync(Arg.Is<AuditLogResponse>(a =>
            a.FeatureFlagId == auditLog.FeatureFlagId &&
            a.Action == AuditLogAction.Create), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleExceptionsGracefully()
    {
        // Arrange
        var logger = Substitute.For<ILogger<AuditLogBackgroundService>>();
        var queueLogger = Substitute.For<ILogger<AuditLogQueue>>();
        var auditLogsService = Substitute.For<IAuditLogsService>();
        var queue = new AuditLogQueue(queueLogger);

        var serviceProvider = Substitute.For<IServiceProvider>();
        var serviceScope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();

        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(serviceScope);
        serviceScope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IAuditLogsService)).Returns(auditLogsService);

        var backgroundService = new AuditLogBackgroundService(queue, serviceProvider, logger);

        var auditLog = new AuditLogResponse
        {
            FeatureFlagId = Guid.NewGuid(),
            Action = AuditLogAction.Create,
            NewStateJson = "{\"enabled\":true}",
            CreatedAt = DateTimeOffset.UtcNow,
            PerformedByUserId = "user123",
            PerformedByUserEmail = "user@example.com"
        };

        // Make the service throw an exception
        auditLogsService.AppendAsync(Arg.Any<AuditLogResponse>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<AuditLogResponse>(new Exception("Test exception")));

        // Act
        await queue.QueueAuditLogAsync(auditLog);

        var cts = new CancellationTokenSource();
        await backgroundService.StartAsync(cts.Token);

        // Give it a moment to process
        await Task.Delay(100, cts.Token);

        // Stop the service
        await cts.CancelAsync();

        // Act - should not throw
        var act = async () => await backgroundService.StopAsync(CancellationToken.None);

        // Assert - should handle exception gracefully
        await act.Should().NotThrowAsync();
        await auditLogsService.Received(1).AppendAsync(Arg.Any<AuditLogResponse>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProcessMultipleAuditLogs()
    {
        // Arrange
        var logger = Substitute.For<ILogger<AuditLogBackgroundService>>();
        var queueLogger = Substitute.For<ILogger<AuditLogQueue>>();
        var auditLogsService = Substitute.For<IAuditLogsService>();
        var queue = new AuditLogQueue(queueLogger);

        var serviceProvider = Substitute.For<IServiceProvider>();
        var serviceScope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();

        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(serviceScope);
        serviceScope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IAuditLogsService)).Returns(auditLogsService);

        var backgroundService = new AuditLogBackgroundService(queue, serviceProvider, logger);

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

        auditLogsService.AppendAsync(Arg.Any<AuditLogResponse>(), Arg.Any<CancellationToken>())
            .Returns(x => Task.FromResult(x.Arg<AuditLogResponse>()));

        // Act
        await queue.QueueAuditLogAsync(auditLog1);
        await queue.QueueAuditLogAsync(auditLog2);

        var cts = new CancellationTokenSource();
        await backgroundService.StartAsync(cts.Token);

        // Give it time to process both
        await Task.Delay(200, cts.Token);

        // Stop the service
        await cts.CancelAsync();
        await backgroundService.StopAsync(CancellationToken.None);

        // Assert
        await auditLogsService.Received(2).AppendAsync(Arg.Any<AuditLogResponse>(), Arg.Any<CancellationToken>());
    }
}