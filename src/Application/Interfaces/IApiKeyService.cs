using Application.DTOs;

namespace Application.Interfaces;

public interface IApiKeyService
{
    Task<ApiKeyCreatedDTO> CreateAsync(Guid projectId, CreateApiKeyDTO dto, string createdByUserId);
    Task<IEnumerable<ApiKeyDTO>> GetByProjectIdAsync(Guid projectId);
    Task RevokeAsync(Guid projectId, Guid apiKeyId);
}