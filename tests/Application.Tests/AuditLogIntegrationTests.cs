using System.Diagnostics;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Mappers;
using Application.Services;
using Contracts.Requests;
using Contracts.Responses;
using Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Application.Tests;

public class AuditLogIntegrationTests
{
    private readonly Guid _testProjectId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task FeatureFlagCreate_ShouldQueueAuditLogWithoutBlocking()
    {
        // Arrange
        var repository = Substitute.For<IFeatureFlagRepository>();
        var mapper = new FeatureFlagMapper();
        var queueLogger = Substitute.For<ILogger<AuditLogQueue>>();
        var queue = new AuditLogQueue(queueLogger);
        var featureFlagsService = new FeatureFlagsService(repository, mapper, queue);

        var dto = new CreateFeatureFlagRequest("test-feature", "Test feature", true, []);

        repository.GetByKeyAsync(_testProjectId, dto.Key).Returns((FeatureFlag?)null);
        repository.CreateAsync(Arg.Any<FeatureFlag>()).Returns(call =>
        {
            var flag = call.Arg<FeatureFlag>();
            flag.Id = Guid.NewGuid();
            flag.ProjectId = _testProjectId;
            flag.CreatedAt = DateTimeOffset.UtcNow;
            return flag;
        });

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await featureFlagsService.CreateAsync(_testProjectId, dto, "user123", "user@example.com");
        stopwatch.Stop();

        // Assert - Feature flag creation should be fast (not waiting for audit log)
        result.Should().NotBeNull();
        result.Key.Should().Be("test-feature");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100); // Should be very fast since audit log is queued

        // Verify audit log was queued
        var channel = queue.GetChannel();
        var hasQueuedLog = channel.Reader.TryRead(out var queuedLog);
        hasQueuedLog.Should().BeTrue();
        queuedLog.Should().NotBeNull();
        queuedLog!.Action.Should().Be(AuditLogAction.Create);
        queuedLog.PerformedByUserId.Should().Be("user123");
        queuedLog.PerformedByUserEmail.Should().Be("user@example.com");
    }

    [Fact]
    public async Task FeatureFlagUpdate_ShouldQueueAuditLogWithoutBlocking()
    {
        // Arrange
        var repository = Substitute.For<IFeatureFlagRepository>();
        var mapper = new FeatureFlagMapper();
        var queueLogger = Substitute.For<ILogger<AuditLogQueue>>();
        var queue = new AuditLogQueue(queueLogger);
        var featureFlagsService = new FeatureFlagsService(repository, mapper, queue);

        const string key = "test-feature";
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

        var dto = new UpdateFeatureFlagRequest("Updated description", true, Array.Empty<FeatureFlagParameters>());

        repository.GetByKeyAsync(_testProjectId, key).Returns(existingFlag);
        repository.UpdateAsync(Arg.Any<FeatureFlag>()).Returns(call => call.Arg<FeatureFlag>());

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await featureFlagsService.UpdateAsync(_testProjectId, key, dto, "admin123", "admin@example.com");
        stopwatch.Stop();

        // Assert - Feature flag update should be fast (not waiting for audit log)
        result.Should().NotBeNull();
        result.Description.Should().Be("Updated description");
        result.Enabled.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);

        // Verify audit log was queued
        var channel = queue.GetChannel();
        var hasQueuedLog = channel.Reader.TryRead(out var queuedLog);
        hasQueuedLog.Should().BeTrue();
        queuedLog.Should().NotBeNull();
        queuedLog!.Action.Should().Be(AuditLogAction.Update);
        queuedLog.PreviousStateJson.Should().NotBeNullOrEmpty();
        queuedLog.NewStateJson.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FeatureFlagDelete_ShouldQueueAuditLogWithoutBlocking()
    {
        // Arrange
        var repository = Substitute.For<IFeatureFlagRepository>();
        var mapper = new FeatureFlagMapper();
        var queueLogger = Substitute.For<ILogger<AuditLogQueue>>();
        var queue = new AuditLogQueue(queueLogger);
        var featureFlagsService = new FeatureFlagsService(repository, mapper, queue);

        const string key = "test-feature";
        var id = Guid.NewGuid();
        var featureFlag = new FeatureFlag
        {
            Id = id,
            ProjectId = _testProjectId,
            Key = key,
            Enabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        repository.GetByKeyAsync(_testProjectId, key).Returns(featureFlag);

        // Act
        var stopwatch = Stopwatch.StartNew();
        await featureFlagsService.DeleteByKeyAsync(_testProjectId, key, "admin123", "admin@example.com");
        stopwatch.Stop();

        // Assert - Feature flag deletion should be fast (not waiting for audit log)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
        await repository.Received(1).DeleteAsync(_testProjectId, id);

        // Verify audit log was queued
        var channel = queue.GetChannel();
        var hasQueuedLog = channel.Reader.TryRead(out var queuedLog);
        hasQueuedLog.Should().BeTrue();
        queuedLog.Should().NotBeNull();
        queuedLog!.Action.Should().Be(AuditLogAction.Delete);
        queuedLog.PreviousStateJson.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BackgroundService_ShouldProcessQueuedLogsFromMultipleOperations()
    {
        // Arrange
        var repository = Substitute.For<IFeatureFlagRepository>();
        var mapper = new FeatureFlagMapper();
        var queueLogger = Substitute.For<ILogger<AuditLogQueue>>();
        var queue = new AuditLogQueue(queueLogger);
        var featureFlagsService = new FeatureFlagsService(repository, mapper, queue);

        var auditLogsService = Substitute.For<IAuditLogsService>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        var serviceScope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();

        serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(serviceScopeFactory);
        serviceScopeFactory.CreateScope().Returns(serviceScope);
        serviceScope.ServiceProvider.Returns(serviceProvider);
        serviceProvider.GetService(typeof(IAuditLogsService)).Returns(auditLogsService);

        var backgroundLogger = Substitute.For<ILogger<AuditLogBackgroundService>>();
        var backgroundService = new AuditLogBackgroundService(queue, serviceProvider, backgroundLogger);

        auditLogsService.AppendAsync(Arg.Any<AuditLogResponse>(), Arg.Any<CancellationToken>())
            .Returns(x => Task.FromResult(x.Arg<AuditLogResponse>()));

        // Configure repository mocks
        repository.GetByKeyAsync(Arg.Any<Guid>(), Arg.Any<string>()).Returns((FeatureFlag?)null);
        repository.CreateAsync(Arg.Any<FeatureFlag>()).Returns(call =>
        {
            var flag = call.Arg<FeatureFlag>();
            flag.Id = Guid.NewGuid();
            flag.ProjectId = _testProjectId;
            flag.CreatedAt = DateTimeOffset.UtcNow;
            return flag;
        });

        // Act - Perform multiple feature flag operations
        var dto1 = new CreateFeatureFlagRequest("feature1", string.Empty, true, []);
        var dto2 = new CreateFeatureFlagRequest("feature2", string.Empty, false, []);

        await featureFlagsService.CreateAsync(_testProjectId, dto1, "user1", "user1@example.com");
        await featureFlagsService.CreateAsync(_testProjectId, dto2, "user2", "user2@example.com");

        // Start background service
        var cts = new CancellationTokenSource();
        var executeTask = backgroundService.StartAsync(cts.Token);

        // Wait for processing
        await Task.Delay(200, cts.Token);

        // Stop the service
        await cts.CancelAsync();
        await backgroundService.StopAsync(CancellationToken.None);

        // Assert - Both audit logs should have been processed
        await auditLogsService.Received(2).AppendAsync(Arg.Any<AuditLogResponse>(), Arg.Any<CancellationToken>());
        await auditLogsService.Received(1).AppendAsync(Arg.Is<AuditLogResponse>(a => a.PerformedByUserId == "user1"),
            Arg.Any<CancellationToken>());
        await auditLogsService.Received(1).AppendAsync(Arg.Is<AuditLogResponse>(a => a.PerformedByUserId == "user2"),
            Arg.Any<CancellationToken>());
    }
}