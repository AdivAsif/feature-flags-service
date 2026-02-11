using System.Text.Json;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain;
using SharedKernel;

namespace Application.Services;

public sealed class FeatureFlagsService(
    IKeyedRepository<FeatureFlag> featureFlagRepository,
    FeatureFlagMapper mapper,
    AuditLogQueue auditLogQueue)
    : IFeatureFlagsService
{
    public async Task<FeatureFlagDTO?> GetAsync(Guid projectId, Guid id)
    {
        // For now, use the base interface method
        var featureFlag = await featureFlagRepository.GetByIdAsync(id);
        if (featureFlag == null || featureFlag.ProjectId != projectId)
            throw new NotFoundException($"Feature Flag with id: {id} not found in project: {projectId}");
        return mapper.FeatureFlagToFeatureFlagDto(featureFlag);
    }

    public async Task<FeatureFlagDTO?> GetByKeyAsync(Guid projectId, string key)
    {
        // Use the project-filtered version of the repository method
        var featureFlag = await featureFlagRepository.GetByKeyAsync(projectId, key);
        if (featureFlag == null)
            throw new NotFoundException($"Feature Flag with key: {key} not found in project: {projectId}");
        return mapper.FeatureFlagToFeatureFlagDto(featureFlag);
    }

    public async Task<IEnumerable<FeatureFlagDTO>> GetAllAsync(Guid projectId, int? take = null, int? skip = null)
    {
        var allFlags = await featureFlagRepository.GetAllAsync(take, skip);
        var projectFlags = allFlags.Where(ff => ff.ProjectId == projectId);
        return mapper.FeatureFlagsToFeatureFlagDtos(projectFlags);
    }

    public async Task<PagedDto<FeatureFlagDTO>> GetPagedAsync(Guid projectId, int first = 10, string? after = null,
        string? before = null)
    {
        // TODO: This needs to be optimized in Phase 4 with proper repository support
        var pagedResult = await featureFlagRepository.GetPagedAsync(first, after, before);
        var projectFlags = pagedResult.Items.Where(ff => ff.ProjectId == projectId);

        return new PagedDto<FeatureFlagDTO>
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

    public async Task<FeatureFlagDTO> CreateAsync(Guid projectId, FeatureFlagDTO featureFlag,
        string? performedByUserId = null,
        string? performedByUserEmail = null)
    {
        if (featureFlag.Key == null) throw new BadRequestException("Feature Flag key is required");

        // Check if key exists in this project
        var existing = await featureFlagRepository.GetByKeyAsync(projectId, featureFlag.Key);
        if (existing != null)
            throw new BadRequestException(
                $"Feature Flag with key: {featureFlag.Key} already exists in project: {projectId}");

        var entityToCreate = mapper.FeatureFlagDtoToFeatureFlag(featureFlag);
        entityToCreate.ProjectId = projectId;
        var created = await featureFlagRepository.CreateAsync(entityToCreate);

        if (string.IsNullOrWhiteSpace(performedByUserId) && string.IsNullOrWhiteSpace(performedByUserEmail))
            return mapper.FeatureFlagToFeatureFlagDto(created);

        var auditLog = new AuditLogDTO
        {
            Action = AuditLogAction.Create,
            FeatureFlagId = created.Id,
            NewStateJson = JsonSerializer.Serialize(created),
            CreatedAt = DateTime.UtcNow,
            PerformedByUserId = performedByUserId ?? string.Empty,
            PerformedByUserEmail = performedByUserEmail ?? string.Empty
        };
        _ = auditLogQueue.QueueAuditLogAsync(auditLog);

        return mapper.FeatureFlagToFeatureFlagDto(created);
    }

    public async Task<FeatureFlagDTO> UpdateAsync(Guid projectId, string key, FeatureFlagDTO featureFlag,
        string? performedByUserId = null, string? performedByUserEmail = null)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new BadRequestException("Feature Flag key is required");

        var featureFlagFromDb = await featureFlagRepository.GetByKeyAsync(projectId, key);
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

        var updated = await featureFlagRepository.UpdateAsync(featureFlagFromDb);

        if (string.IsNullOrWhiteSpace(performedByUserId) && string.IsNullOrWhiteSpace(performedByUserEmail))
            return mapper.FeatureFlagToFeatureFlagDto(updated);

        var auditLog = new AuditLogDTO
        {
            FeatureFlagId = featureFlagFromDb.Id,
            Action = AuditLogAction.Update,
            NewStateJson = JsonSerializer.Serialize(updated),
            PreviousStateJson = previousStateJson,
            CreatedAt = DateTime.UtcNow,
            PerformedByUserId = performedByUserId ?? string.Empty,
            PerformedByUserEmail = performedByUserEmail ?? string.Empty
        };
        _ = auditLogQueue.QueueAuditLogAsync(auditLog);

        return mapper.FeatureFlagToFeatureFlagDto(updated);
    }

    public async Task DeleteByKeyAsync(Guid projectId, string key, string? performedByUserId = null,
        string? performedByUserEmail = null)
    {
        var featureFlag = await featureFlagRepository.GetByKeyAsync(projectId, key);
        if (featureFlag == null)
            throw new NotFoundException($"Feature Flag with key: {key} not found in project: {projectId}");
        await featureFlagRepository.DeleteAsync(featureFlag.Id);

        if (string.IsNullOrWhiteSpace(performedByUserId) && string.IsNullOrWhiteSpace(performedByUserEmail)) return;
        var auditLog = new AuditLogDTO
        {
            Action = AuditLogAction.Delete,
            FeatureFlagId = featureFlag.Id,
            CreatedAt = DateTime.UtcNow,
            PreviousStateJson = JsonSerializer.Serialize(featureFlag),
            PerformedByUserId = performedByUserId ?? string.Empty,
            PerformedByUserEmail = performedByUserEmail ?? string.Empty
        };
        _ = auditLogQueue.QueueAuditLogAsync(auditLog);
    }
}