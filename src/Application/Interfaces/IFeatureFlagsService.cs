using Application.DTOs;

namespace Application.Interfaces;

public interface IFeatureFlagsService
{
    Task<FeatureFlagDto?> GetAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);
    Task<FeatureFlagDto?> GetByKeyAsync(Guid projectId, string key, CancellationToken cancellationToken = default);

    Task<PagedDto<FeatureFlagDto>> GetPagedAsync(Guid projectId, int first = 10, string? after = null,
        string? before = null, CancellationToken cancellationToken = default);

    Task<FeatureFlagDto> CreateAsync(Guid projectId, FeatureFlagDto featureFlag,
        string? performedByUserId = null,
        string? performedByUserEmail = null, CancellationToken cancellationToken = default);

    Task<FeatureFlagDto> UpdateAsync(Guid projectId, string key, FeatureFlagDto featureFlag,
        string? performedByUserId = null,
        string? performedByUserEmail = null, CancellationToken cancellationToken = default);

    Task DeleteByKeyAsync(Guid projectId, string key, string? performedByUserId = null,
        string? performedByUserEmail = null, CancellationToken cancellationToken = default);
}