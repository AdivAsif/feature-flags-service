using Domain;

namespace Application.Interfaces.Repositories;

/// <summary>
///     Repository interface for ApiKey operations.
///     Can be decorated with caching using Scrutor.
/// </summary>
public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(Guid apiKeyId, CancellationToken cancellationToken = default);
    Task<ApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default);
    Task<IEnumerable<ApiKey>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid apiKeyId, CancellationToken cancellationToken = default);
    Task UpdateLastUsedAtAsync(Guid apiKeyId, CancellationToken cancellationToken = default);
}