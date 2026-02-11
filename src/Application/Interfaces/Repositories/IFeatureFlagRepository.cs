using Domain;
using SharedKernel;

namespace Application.Interfaces.Repositories;

/// <summary>
///     Repository interface for FeatureFlag operations.
/// </summary>
public interface IFeatureFlagRepository
{
    Task<FeatureFlag?> GetByIdAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);
    Task<FeatureFlag?> GetByKeyAsync(Guid projectId, string key, CancellationToken cancellationToken = default);

    Task<PagedResult<FeatureFlag>> GetPagedAsync(Guid projectId, int first = 10, string? after = null,
        string? before = null, CancellationToken cancellationToken = default);

    Task<FeatureFlag> CreateAsync(FeatureFlag featureFlag, CancellationToken cancellationToken = default);
    Task<FeatureFlag> UpdateAsync(FeatureFlag featureFlag, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);
}