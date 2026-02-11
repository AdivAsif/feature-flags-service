using Application.DTOs;

namespace Application.Interfaces;

public interface IApiKeyService
{
    Task<ApiKeyCreatedDTO> CreateAsync(Guid projectId, CreateApiKeyDTO dto, string createdByUserId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ApiKeyDto>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid projectId, Guid apiKeyId, CancellationToken cancellationToken = default);
}