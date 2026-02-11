using FeatureFlags.Client.Models;

namespace FeatureFlags.Client;

public interface IFeatureFlagsManagementClient
{
    // Projects
    Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken cancellationToken = default);

    // API Keys
    Task<IReadOnlyList<ApiKey>> GetApiKeysByProjectIdAsync(Guid projectId,
        CancellationToken cancellationToken = default);

    // Feature Flags
    Task<PagedResult<FeatureFlag>> GetFeatureFlagsAsync(
        int first = 10,
        string? after = null,
        string? before = null,
        CancellationToken cancellationToken = default);

    Task<FeatureFlag> GetFeatureFlagByKeyAsync(string key, CancellationToken cancellationToken = default);
}