using System.Text.Json;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;

namespace Application.Services;

public sealed class FeatureFlagsService(
    IFeatureFlagRepository featureFlagRepository,
    FeatureFlagMapper mapper,
    AuditLogQueue auditLogQueue)
    : IFeatureFlagsService
{
    public async Task<FeatureFlagDto?> GetAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default)
    {
        var featureFlag = await featureFlagRepository.GetByIdAsync(projectId, id, cancellationToken);
        return featureFlag == null
            ? throw new NotFoundException($"Feature Flag with id: {id} not found in project: {projectId}")
            : mapper.FeatureFlagToFeatureFlagDto(featureFlag);
    }

    public async Task<FeatureFlagDto?> GetByKeyAsync(Guid projectId, string key,
        CancellationToken cancellationToken = default)
    {
        // Use the project-filtered version of the repository method
        var featureFlag = await featureFlagRepository.GetByKeyAsync(projectId, key, cancellationToken);
        return featureFlag == null
            ? throw new NotFoundException($"Feature Flag with key: {key} not found in project: {projectId}")
            : mapper.FeatureFlagToFeatureFlagDto(featureFlag);
    }

    public async Task<PagedDto<FeatureFlagDto>> GetPagedAsync(Guid projectId, int first = 10, string? after = null,
        string? before = null, CancellationToken cancellationToken = default)
    {
        var pagedResult = await featureFlagRepository.GetPagedAsync(projectId, first, after, before, cancellationToken);

        return new PagedDto<FeatureFlagDto>
        {
            Items = mapper.FeatureFlagsToFeatureFlagDtos(pagedResult.Items),
            PageInfo = new PaginationInfo
            {
                HasNextPage = pagedResult.PageInfo.HasNextPage,
                HasPreviousPage = pagedResult.PageInfo.HasPreviousPage,
                StartCursor = pagedResult.PageInfo.StartCursor,
                EndCursor = pagedResult.PageInfo.EndCursor,
                TotalCount = pagedResult.PageInfo.TotalCount
            }
        };
    }

    public async Task<FeatureFlagDto> CreateAsync(Guid projectId, FeatureFlagDto featureFlag,
        string? performedByUserId = null,
        string? performedByUserEmail = null, CancellationToken cancellationToken = default)
    {
        if (featureFlag.Key == null) throw new BadRequestException("Feature Flag key is required");

        // Check if key exists in this project
        var existing = await featureFlagRepository.GetByKeyAsync(projectId, featureFlag.Key, cancellationToken);
        if (existing != null)
            throw new BadRequestException(
                $"Feature Flag with key: {featureFlag.Key} already exists in project: {projectId}");

        var entityToCreate = mapper.FeatureFlagDtoToFeatureFlag(featureFlag);
        entityToCreate.ProjectId = projectId;
        var created = await featureFlagRepository.CreateAsync(entityToCreate, cancellationToken);

        if (string.IsNullOrWhiteSpace(performedByUserId) && string.IsNullOrWhiteSpace(performedByUserEmail))
            return mapper.FeatureFlagToFeatureFlagDto(created);

        var auditLog = new AuditLogDto
        {
            Action = AuditLogAction.Create,
            FeatureFlagId = created.Id,
            NewStateJson = JsonSerializer.Serialize(created),
            CreatedAt = DateTime.UtcNow,
            PerformedByUserId = performedByUserId ?? string.Empty,
            PerformedByUserEmail = performedByUserEmail ?? string.Empty
        };
        _ = auditLogQueue.QueueAuditLogAsync(auditLog, cancellationToken);

        return mapper.FeatureFlagToFeatureFlagDto(created);
    }

    public async Task<FeatureFlagDto> UpdateAsync(Guid projectId, string key, FeatureFlagDto featureFlag,
        string? performedByUserId = null, string? performedByUserEmail = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new BadRequestException("Feature Flag key is required");

        var featureFlagFromDb = await featureFlagRepository.GetByKeyAsync(projectId, key, cancellationToken);
        if (featureFlagFromDb == null)
            throw new NotFoundException($"Feature Flag with key: {featureFlag.Key} not found in project: {projectId}");

        // Disallow changing the canonical key via update
        if (!string.IsNullOrEmpty(featureFlag.Key) && !string.Equals(featureFlag.Key, key, StringComparison.Ordinal))
            throw new BadRequestException(
                "Changing a Feature Flag key is not allowed. Create a new flag or use an explicit rename operation.");

        var previousStateJson = JsonSerializer.Serialize(featureFlagFromDb);

        featureFlagFromDb.Description = featureFlag.Description;
        featureFlagFromDb.Enabled = featureFlag.Enabled;
        featureFlagFromDb.Parameters = featureFlag.Parameters;
        featureFlagFromDb.Version++;
        featureFlagFromDb.UpdatedAt = DateTime.UtcNow;

        var updated = await featureFlagRepository.UpdateAsync(featureFlagFromDb, cancellationToken);

        if (string.IsNullOrWhiteSpace(performedByUserId) && string.IsNullOrWhiteSpace(performedByUserEmail))
            return mapper.FeatureFlagToFeatureFlagDto(updated);

        var auditLog = new AuditLogDto
        {
            FeatureFlagId = featureFlagFromDb.Id,
            Action = AuditLogAction.Update,
            NewStateJson = JsonSerializer.Serialize(updated),
            PreviousStateJson = previousStateJson,
            CreatedAt = DateTime.UtcNow,
            PerformedByUserId = performedByUserId ?? string.Empty,
            PerformedByUserEmail = performedByUserEmail ?? string.Empty
        };
        _ = auditLogQueue.QueueAuditLogAsync(auditLog, cancellationToken);

        return mapper.FeatureFlagToFeatureFlagDto(updated);
    }

    public async Task DeleteByKeyAsync(Guid projectId, string key, string? performedByUserId = null,
        string? performedByUserEmail = null, CancellationToken cancellationToken = default)
    {
        var featureFlag = await featureFlagRepository.GetByKeyAsync(projectId, key, cancellationToken);
        if (featureFlag == null)
            throw new NotFoundException($"Feature Flag with key: {key} not found in project: {projectId}");
        await featureFlagRepository.DeleteAsync(projectId, featureFlag.Id, cancellationToken);

        if (string.IsNullOrWhiteSpace(performedByUserId) && string.IsNullOrWhiteSpace(performedByUserEmail)) return;
        var auditLog = new AuditLogDto
        {
            Action = AuditLogAction.Delete,
            FeatureFlagId = featureFlag.Id,
            CreatedAt = DateTime.UtcNow,
            PreviousStateJson = JsonSerializer.Serialize(featureFlag),
            PerformedByUserId = performedByUserId ?? string.Empty,
            PerformedByUserEmail = performedByUserEmail ?? string.Empty
        };
        _ = auditLogQueue.QueueAuditLogAsync(auditLog, cancellationToken);
    }
}