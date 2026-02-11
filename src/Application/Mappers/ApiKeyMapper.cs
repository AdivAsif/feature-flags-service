using Contracts.Requests;
using Contracts.Responses;
using Domain;
using Riok.Mapperly.Abstractions;

namespace Application.Mappers;

[Mapper]
public partial class ApiKeyMapper
{
    public partial ApiKeyResponse ApiKeyToResponse(ApiKey entity);
    public partial IEnumerable<ApiKeyResponse> ApiKeysToResponses(IEnumerable<ApiKey> entities);

    public partial ApiKey CreateRequestToEntity(CreateApiKeyRequest request);

    public partial ApiKeyCreatedResponse ApiKeyToCreatedResponse(ApiKey entity, string apiKey);
}