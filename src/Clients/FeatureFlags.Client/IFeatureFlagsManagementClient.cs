using Contracts.Common;
using Contracts.Requests;
using Contracts.Responses;

namespace FeatureFlags.Client;

/// <summary>
///     Interface for managing projects, API keys, and feature flags.
/// </summary>
public interface IFeatureFlagsManagementClient
{
    /// <summary>
    ///     Gets a list of all projects. You need a valid JWT Bearer token with the role "admin" to access this endpoint.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of projects.</returns>
    Task<IReadOnlyList<ProjectResponse>> GetProjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a new project. You need a valid JWT Bearer token with the role "admin" to access this endpoint.
    /// </summary>
    /// <param name="request">The project creation request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created project.</returns>
    Task<ProjectResponse> CreateProjectAsync(CreateProjectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an existing project. You need a valid JWT Bearer token with the role "admin" to access this endpoint.
    /// </summary>
    /// <param name="projectId">The ID of the project to update.</param>
    /// <param name="request">The update details.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated project.</returns>
    Task<ProjectResponse> UpdateProjectAsync(Guid projectId, UpdateProjectRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a project by its ID. You need a valid JWT Bearer token with the role "admin" to access this endpoint.
    /// </summary>
    /// <param name="projectId">The ID of the project to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets all API keys associated with a specific project. You need a valid JWT Bearer token with the role "admin" to
    ///     access this endpoint.
    /// </summary>
    /// <param name="projectId">The project ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of API keys.</returns>
    Task<IReadOnlyList<ApiKeyResponse>> GetApiKeysByProjectIdAsync(Guid projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a new API key for a project. You need a valid JWT Bearer token with the role "admin" to access this
    ///     endpoint.
    /// </summary>
    /// <param name="projectId">The project ID.</param>
    /// <param name="request">The API key creation request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created API key.</returns>
    Task<ApiKeyResponse> CreateApiKeyAsync(Guid projectId, CreateApiKeyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Revokes an API key. You need a valid JWT Bearer token with the role "admin" to access this endpoint.
    /// </summary>
    /// <param name="projectId">The project ID.</param>
    /// <param name="keyId">The ID of the API key to revoke.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RevokeApiKeyAsync(Guid projectId, Guid keyId, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a paged list of feature flags. A valid API key with the "flags:read" scope is required to access this
    ///     endpoint.
    /// </summary>
    /// <param name="first">The number of items to return (default: 10).</param>
    /// <param name="after">Cursor for the next page.</param>
    /// <param name="before">Cursor for the previous page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged result of feature flags.</returns>
    Task<PagedResult<FeatureFlagResponse>> GetFeatureFlagsAsync(
        int first = 10,
        string? after = null,
        string? before = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets a specific feature flag by its unique key. A valid API key with the "flags:read" scope is required to access
    ///     this endpoint.
    /// </summary>
    /// <param name="key">The unique key of the feature flag.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The feature flag details.</returns>
    Task<FeatureFlagResponse> GetFeatureFlagByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Creates a new feature flag. A valid API key with the "flags:write" scope is required to access this endpoint.
    /// </summary>
    /// <param name="request">The feature flag creation request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created feature flag.</returns>
    Task<FeatureFlagResponse> CreateFeatureFlagAsync(CreateFeatureFlagRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates an existing feature flag. A valid API key with the "flags:write" scope is required to access this endpoint.
    /// </summary>
    /// <param name="key">The unique key of the feature flag to update.</param>
    /// <param name="request">The update details.</param>
    /// <param name="ifMatch">Optional ETag for concurrency control.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated feature flag.</returns>
    Task<FeatureFlagResponse> UpdateFeatureFlagAsync(string key, UpdateFeatureFlagRequest request,
        string? ifMatch = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a feature flag by its unique key. A valid API key with the "flags:delete" scope is required to access this
    ///     endpoint.
    /// </summary>
    /// <param name="key">The unique key of the feature flag.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DeleteFeatureFlagAsync(string key, CancellationToken cancellationToken = default);
}