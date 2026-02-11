using Application.Common;
using Contracts.Requests;
using Contracts.Responses;

namespace Application.Interfaces;

public interface IFeatureFlagsService
{
    Task<FeatureFlagResponse?> GetAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default);
    Task<FeatureFlagResponse?> GetByKeyAsync(Guid projectId, string key, CancellationToken cancellationToken = default);

    Task<Slice<FeatureFlagResponse>> GetPagedAsync(Guid projectId, int first = 10, string? after = null,
        string? before = null, CancellationToken cancellationToken = default);

    Task<FeatureFlagResponse> CreateAsync(Guid projectId, CreateFeatureFlagRequest featureFlag,
        string? performedByUserId = null,
        string? performedByUserEmail = null, CancellationToken cancellationToken = default);

    Task<FeatureFlagResponse> UpdateAsync(Guid projectId, string key, UpdateFeatureFlagRequest featureFlag,
        string? performedByUserId = null,
        string? performedByUserEmail = null, CancellationToken cancellationToken = default);

    Task DeleteByKeyAsync(Guid projectId, string key, string? performedByUserId = null,
        string? performedByUserEmail = null, CancellationToken cancellationToken = default);
}