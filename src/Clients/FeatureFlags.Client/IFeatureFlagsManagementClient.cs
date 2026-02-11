namespace FeatureFlags.Client;

public interface IFeatureFlagsManagementClient
{
    Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApiKey>> GetApiKeysByProjectIdAsync(Guid projectId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<FeatureFlag>> GetFeatureFlagsAsync(
        int first = 10,
        string? after = null,
        string? before = null,
        CancellationToken cancellationToken = default);

    Task<FeatureFlag> GetFeatureFlagByKeyAsync(string key, CancellationToken cancellationToken = default);
}