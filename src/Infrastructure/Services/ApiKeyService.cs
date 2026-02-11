using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Contracts.Requests;
using Contracts.Responses;
using Domain;
using Infrastructure.Authentication;

namespace Infrastructure.Services;

public sealed class ApiKeyService(IApiKeyRepository apiKeyRepository, IProjectRepository projectRepository)
    : IApiKeyService
{
    // GET
    public async Task<IEnumerable<ApiKeyResponse>> GetByProjectIdAsync(Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project == null)
            throw new NotFoundException($"Project with id: {projectId} not found");

        var apiKeys = await apiKeyRepository.GetByProjectIdAsync(projectId, cancellationToken);

        return apiKeys.Select(k => new ApiKeyResponse
        {
            Id = k.Id,
            ProjectId = k.ProjectId,
            KeyPrefix = k.KeyPrefix, // Only return prefix for security
            Name = k.Name,
            Scopes = k.Scopes,
            ExpiresAt = k.ExpiresAt,
            LastUsedAt = k.LastUsedAt,
            CreatedByUserId = k.CreatedByUserId,
            RevokedAt = k.RevokedAt,
            IsActive = k.IsActive,
            CreatedAt = k.CreatedAt,
            UpdatedAt = k.UpdatedAt
        });
    }
    
    // CREATE
    public async Task<ApiKeyCreatedResponse> CreateAsync(Guid projectId, CreateApiKeyRequest dto,
        string createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project == null)
            throw new NotFoundException($"Project with id: {projectId} not found");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BadRequestException("API key name is required");

        if (string.IsNullOrWhiteSpace(dto.Scopes))
            throw new BadRequestException("API key scopes are required");

        // Generate the API key
        var apiKey = ApiKeyGenerator.GenerateKey();
        var keyHash = ApiKeyHasher.HashKey(apiKey);
        var keyPrefix = ApiKeyGenerator.ExtractPrefix(apiKey);

        var apiKeyEntity = new ApiKey
        {
            ProjectId = projectId,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            Name = dto.Name,
            Scopes = dto.Scopes,
            ExpiresAt = dto.ExpiresAt,
            CreatedByUserId = createdByUserId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var created = await apiKeyRepository.CreateAsync(apiKeyEntity, cancellationToken);

        return new ApiKeyCreatedResponse(
            created.Id,
            apiKey, // Return the plain key only once
            created.Name,
            created.Scopes,
            created.ExpiresAt,
            created.CreatedAt
        );
    }

    // UPDATE (soft delete)
    public async Task RevokeAsync(Guid projectId, Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project == null)
            throw new NotFoundException($"Project with id: {projectId} not found");

        await apiKeyRepository.RevokeAsync(apiKeyId, cancellationToken);
    }
}