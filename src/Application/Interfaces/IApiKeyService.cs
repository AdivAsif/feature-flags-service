using Contracts.Requests;
using Contracts.Responses;

namespace Application.Interfaces;

public interface IApiKeyService
{
    Task<IEnumerable<ApiKeyResponse>>
        GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ApiKeyCreatedResponse> CreateAsync(Guid projectId, CreateApiKeyRequest dto, string createdByUserId,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(Guid projectId, Guid apiKeyId, CancellationToken cancellationToken = default);
}