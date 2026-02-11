using Application.Common;
using Domain;

namespace Application.Interfaces.Repositories;

// Repository interface for FeatureFlag persistence
// Can be decorated with caching using Scrutor, implemented in the Infrastructure layer
public interface IFeatureFlagRepository
{
    Task<FeatureFlag?> GetByIdAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);
    Task<FeatureFlag?> GetByKeyAsync(Guid projectId, string key, CancellationToken cancellationToken = default);

    Task<Slice<FeatureFlag>> GetPagedAsync(Guid projectId, int first = 10, string? after = null,
        string? before = null, CancellationToken cancellationToken = default);

    Task<FeatureFlag> CreateAsync(FeatureFlag featureFlag, CancellationToken cancellationToken = default);
    Task<FeatureFlag> UpdateAsync(FeatureFlag featureFlag, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);
}