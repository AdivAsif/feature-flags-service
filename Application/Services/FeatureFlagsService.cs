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
    IAuditLogsService auditLogsService)
    : IFeatureFlagsService
{
    public async Task<FeatureFlagDTO?> GetAsync(Guid id)
    {
        var featureFlag = await featureFlagRepository.GetByIdAsync(id);
        return featureFlag == null
            ? throw new NotFoundException($"Feature Flag with id: {id} not found")
            : mapper.FeatureFlagToFeatureFlagDto(featureFlag);
    }

    public async Task<FeatureFlagDTO?> GetByKeyAsync(string key)
    {
        var featureFlag = await featureFlagRepository.GetByKeyAsync(key);
        if (featureFlag == null)
            throw new NotFoundException($"Feature Flag with key: {key} not found");
        return mapper.FeatureFlagToFeatureFlagDto(featureFlag);
    }

    public async Task<IEnumerable<FeatureFlagDTO>> GetAllAsync(int? take = null, int? skip = null)
    {
        return mapper.FeatureFlagsToFeatureFlagDtos(await featureFlagRepository.GetAllAsync(take, skip));
    }

    public async Task<PagedDto<FeatureFlagDTO>> GetPagedAsync(int first = 10, string? after = null,
        string? before = null)
    {
        var pagedResult = await featureFlagRepository.GetPagedAsync(first, after, before);

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

    public async Task<FeatureFlagDTO> CreateAsync(FeatureFlagDTO featureFlag, string? performedByUserId = null,
        string? performedByUserEmail = null)
    {
        if (featureFlag.Key == null) throw new BadRequestException("Feature Flag key is required");
        if (await featureFlagRepository.GetByKeyAsync(featureFlag.Key) != null)
            throw new BadRequestException($"Feature Flag with key: {featureFlag.Key} already exists");

        var entityToCreate = mapper.FeatureFlagDtoToFeatureFlag(featureFlag);
        var created = await featureFlagRepository.CreateAsync(entityToCreate);

        if (performedByUserId == null || performedByUserEmail == null)
            return mapper.FeatureFlagToFeatureFlagDto(created);

        var auditLog = new AuditLogDTO
        {
            Action = AuditLogAction.Create,
            FeatureFlagId = created.Id,
            NewStateJson = JsonSerializer.Serialize(created),
            CreatedAt = DateTime.UtcNow,
            PerformedByUserId = performedByUserId,
            PerformedByUserEmail = performedByUserEmail
        };
        await auditLogsService.AppendAsync(auditLog);

        return mapper.FeatureFlagToFeatureFlagDto(created);
    }

    public async Task<FeatureFlagDTO> UpdateAsync(string key, FeatureFlagDTO featureFlag,
        string? performedByUserId = null, string? performedByUserEmail = null)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new BadRequestException("Feature Flag key is required");

        var featureFlagFromDb = await featureFlagRepository.GetByKeyAsync(key);
        if (featureFlagFromDb == null)
            throw new NotFoundException($"Feature Flag with key: {featureFlag.Key} not found");

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

        if (performedByUserId == null || performedByUserEmail == null)
            return mapper.FeatureFlagToFeatureFlagDto(updated);

        var auditLog = new AuditLogDTO
        {
            FeatureFlagId = featureFlagFromDb.Id,
            Action = AuditLogAction.Update,
            NewStateJson = JsonSerializer.Serialize(updated),
            PreviousStateJson = previousStateJson,
            CreatedAt = DateTime.UtcNow,
            PerformedByUserId = performedByUserId,
            PerformedByUserEmail = performedByUserEmail
        };
        await auditLogsService.AppendAsync(auditLog);

        return mapper.FeatureFlagToFeatureFlagDto(updated);
    }

    public async Task DeleteByKeyAsync(string key, string? performedByUserId = null,
        string? performedByUserEmail = null)
    {
        var featureFlag = await featureFlagRepository.GetByKeyAsync(key);
        if (featureFlag == null)
            throw new NotFoundException($"Feature Flag with key: {key} not found");
        await featureFlagRepository.DeleteAsync(featureFlag.Id);

        if (performedByUserId == null || performedByUserEmail == null) return;
        var auditLog = new AuditLogDTO
        {
            Action = AuditLogAction.Delete,
            FeatureFlagId = featureFlag.Id,
            CreatedAt = DateTime.UtcNow,
            PreviousStateJson = JsonSerializer.Serialize(featureFlag),
            PerformedByUserId = performedByUserId,
            PerformedByUserEmail = performedByUserEmail
        };
        await auditLogsService.AppendAsync(auditLog);
    }
}