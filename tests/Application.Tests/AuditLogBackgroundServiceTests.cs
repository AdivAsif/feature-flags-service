using Application.DTOs;
using Application.Interfaces;
using Application.Services;
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

        var auditLog = new AuditLogDTO
        {
            FeatureFlagId = Guid.NewGuid(),
            Action = AuditLogAction.Create,
            NewStateJson = "{\"enabled\":true}",
            CreatedAt = DateTime.UtcNow,
            PerformedByUserId = "user123",
            PerformedByUserEmail = "user@example.com"
        };

        auditLogsService.AppendAsync(Arg.Any<AuditLogDTO>())
            .Returns(Task.FromResult(auditLog));

        // Act
        await queue.QueueAuditLogAsync(auditLog);

        var cts = new CancellationTokenSource();
        var executeTask = backgroundService.StartAsync(cts.Token);

        // Give it a moment to process
        await Task.Delay(100);

        // Stop the service
        cts.Cancel();
        await backgroundService.StopAsync(CancellationToken.None);

        // Assert
        await auditLogsService.Received(1).AppendAsync(Arg.Is<AuditLogDTO>(a =>
            a.FeatureFlagId == auditLog.FeatureFlagId &&
            a.Action == AuditLogAction.Create));
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

        var auditLog = new AuditLogDTO
        {
            FeatureFlagId = Guid.NewGuid(),
            Action = AuditLogAction.Create,
            NewStateJson = "{\"enabled\":true}",
            CreatedAt = DateTime.UtcNow,
            PerformedByUserId = "user123",
            PerformedByUserEmail = "user@example.com"
        };

        // Make the service throw an exception
        auditLogsService.AppendAsync(Arg.Any<AuditLogDTO>())
            .Returns<AuditLogDTO>(_ => throw new Exception("Test exception"));

        // Act
        await queue.QueueAuditLogAsync(auditLog);

        var cts = new CancellationTokenSource();
        var executeTask = backgroundService.StartAsync(cts.Token);

        // Give it a moment to process
        await Task.Delay(100);

        // Stop the service
        cts.Cancel();

        // Act - should not throw
        var act = async () => await backgroundService.StopAsync(CancellationToken.None);

        // Assert - should handle exception gracefully
        await act.Should().NotThrowAsync();
        await auditLogsService.Received(1).AppendAsync(Arg.Any<AuditLogDTO>());
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

        var auditLog1 = new AuditLogDTO
        {
            FeatureFlagId = Guid.NewGuid(),
            Action = AuditLogAction.Create,
            NewStateJson = "{\"enabled\":true}",
            CreatedAt = DateTime.UtcNow,
            PerformedByUserId = "user1",
            PerformedByUserEmail = "user1@example.com"
        };

        var auditLog2 = new AuditLogDTO
        {
            FeatureFlagId = Guid.NewGuid(),
            Action = AuditLogAction.Update,
            PreviousStateJson = "{\"enabled\":true}",
            NewStateJson = "{\"enabled\":false}",
            CreatedAt = DateTime.UtcNow,
            PerformedByUserId = "user2",
            PerformedByUserEmail = "user2@example.com"
        };

        auditLogsService.AppendAsync(Arg.Any<AuditLogDTO>())
            .Returns(x => Task.FromResult(x.Arg<AuditLogDTO>()));

        // Act
        await queue.QueueAuditLogAsync(auditLog1);
        await queue.QueueAuditLogAsync(auditLog2);

        var cts = new CancellationTokenSource();
        var executeTask = backgroundService.StartAsync(cts.Token);

        // Give it time to process both
        await Task.Delay(200);

        // Stop the service
        cts.Cancel();
        await backgroundService.StopAsync(CancellationToken.None);

        // Assert
        await auditLogsService.Received(2).AppendAsync(Arg.Any<AuditLogDTO>());
    }
}