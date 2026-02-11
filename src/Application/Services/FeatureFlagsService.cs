using System.Text.Json;
using Application.Common;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Mappers;
using Contracts.Requests;
using Contracts.Responses;
using Domain;

namespace Application.Services;

public sealed class FeatureFlagsService(
    IFeatureFlagRepository featureFlagRepository,
    FeatureFlagMapper mapper,
    AuditLogQueue auditLogQueue)
    : IFeatureFlagsService
{
    public async Task<FeatureFlagResponse?> GetAsync(Guid projectId, Guid id,
        CancellationToken cancellationToken = default)
    {
        var featureFlag = await featureFlagRepository.GetByIdAsync(projectId, id, cancellationToken);
        return featureFlag == null
            ? throw new NotFoundException($"Feature Flag with id: {id} not found in project: {projectId}")
            : mapper.FeatureFlagToResponse(featureFlag);
    }

    public async Task<FeatureFlagResponse?> GetByKeyAsync(Guid projectId, string key,
        CancellationToken cancellationToken = default)
    {
        var featureFlag = await featureFlagRepository.GetByKeyAsync(projectId, key, cancellationToken);
        return featureFlag == null
            ? throw new NotFoundException($"Feature Flag with key: {key} not found in project: {projectId}")
            : mapper.FeatureFlagToResponse(featureFlag);
    }

    public async Task<Slice<FeatureFlagResponse>> GetPagedAsync(Guid projectId, int first = 10,
        string? after = null,
        string? before = null, CancellationToken cancellationToken = default)
    {
        var pagedResult = await featureFlagRepository.GetPagedAsync(projectId, first, after, before, cancellationToken);

        return new Slice<FeatureFlagResponse>
        {
            Items = mapper.FeatureFlagsToResponses(pagedResult.Items).ToList(),
            StartCursor = pagedResult.StartCursor,
            EndCursor = pagedResult.EndCursor,
            HasNextPage = pagedResult.HasNextPage,
            HasPreviousPage = pagedResult.HasPreviousPage,
            TotalCount = pagedResult.TotalCount
        };
    }

    public async Task<FeatureFlagResponse> CreateAsync(Guid projectId, CreateFeatureFlagRequest request,
        string? performedByUserId = null,
        string? performedByUserEmail = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Key)) throw new BadRequestException("Feature Flag key is required");

        var existing = await featureFlagRepository.GetByKeyAsync(projectId, request.Key, cancellationToken);
        if (existing != null)
            throw new BadRequestException(
                $"Feature Flag with key: {request.Key} already exists in project: {projectId}");

        var entityToCreate = mapper.CreateRequestToEntity(request);
        entityToCreate.ProjectId = projectId;
        var created = await featureFlagRepository.CreateAsync(entityToCreate, cancellationToken);

        if (string.IsNullOrWhiteSpace(performedByUserId) && string.IsNullOrWhiteSpace(performedByUserEmail))
            return mapper.FeatureFlagToResponse(created);

        var auditLog = new AuditLogResponse
        {
            Action = AuditLogAction.Create,
            FeatureFlagId = created.Id,
            NewStateJson = JsonSerializer.Serialize(created),
            CreatedAt = DateTime.UtcNow,
            PerformedByUserId = performedByUserId ?? string.Empty,
            PerformedByUserEmail = performedByUserEmail ?? string.Empty
        };
        _ = auditLogQueue.QueueAuditLogAsync(auditLog, cancellationToken);

        return mapper.FeatureFlagToResponse(created);
    }

    public async Task<FeatureFlagResponse> UpdateAsync(Guid projectId, string key, UpdateFeatureFlagRequest request,
        string? performedByUserId = null, string? performedByUserEmail = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new BadRequestException("Feature Flag key is required");

        var featureFlagFromDb = await featureFlagRepository.GetByKeyAsync(projectId, key, cancellationToken);
        if (featureFlagFromDb == null)
            throw new NotFoundException($"Feature Flag with key: {key} not found in project: {projectId}");

        var previousStateJson = JsonSerializer.Serialize(featureFlagFromDb);

        mapper.UpdateRequestToEntity(request, featureFlagFromDb);
        featureFlagFromDb.Version++;
        featureFlagFromDb.UpdatedAt = DateTime.UtcNow;

        var updated = await featureFlagRepository.UpdateAsync(featureFlagFromDb, cancellationToken);

        if (string.IsNullOrWhiteSpace(performedByUserId) && string.IsNullOrWhiteSpace(performedByUserEmail))
            return mapper.FeatureFlagToResponse(updated);

        var auditLog = new AuditLogResponse
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

        return mapper.FeatureFlagToResponse(updated);
    }

    public async Task DeleteByKeyAsync(Guid projectId, string key, string? performedByUserId = null,
        string? performedByUserEmail = null, CancellationToken cancellationToken = default)
    {
        var featureFlag = await featureFlagRepository.GetByKeyAsync(projectId, key, cancellationToken);
        if (featureFlag == null)
            throw new NotFoundException($"Feature Flag with key: {key} not found in project: {projectId}");
        await featureFlagRepository.DeleteAsync(projectId, featureFlag.Id, cancellationToken);

        if (string.IsNullOrWhiteSpace(performedByUserId) && string.IsNullOrWhiteSpace(performedByUserEmail)) return;
        var auditLog = new AuditLogResponse
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