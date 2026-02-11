using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain;
using Infrastructure.Authentication;
using Infrastructure.Repositories;

namespace Infrastructure.Services;

public sealed class ApiKeyService : IApiKeyService
{
    private readonly ApiKeyRepository _apiKeyRepository;
    private readonly ProjectRepository _projectRepository;

    public ApiKeyService(ApiKeyRepository apiKeyRepository, ProjectRepository projectRepository)
    {
        _apiKeyRepository = apiKeyRepository;
        _projectRepository = projectRepository;
    }

    public async Task<ApiKeyCreatedDTO> CreateAsync(Guid projectId, CreateApiKeyDTO dto, string createdByUserId)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
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

        var created = await _apiKeyRepository.CreateAsync(apiKeyEntity);

        return new ApiKeyCreatedDTO
        {
            Id = created.Id,
            ApiKey = apiKey, // Return the plain key only once
            Name = created.Name,
            Scopes = created.Scopes,
            ExpiresAt = created.ExpiresAt,
            CreatedAt = created.CreatedAt
        };
    }

    public async Task<IEnumerable<ApiKeyDTO>> GetByProjectIdAsync(Guid projectId)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            throw new NotFoundException($"Project with id: {projectId} not found");

        var apiKeys = await _apiKeyRepository.GetByProjectIdAsync(projectId);

        return apiKeys.Select(k => new ApiKeyDTO
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

    public async Task RevokeAsync(Guid projectId, Guid apiKeyId)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
            throw new NotFoundException($"Project with id: {projectId} not found");

        // Note: We could add validation here to ensure the API key belongs to the project
        await _apiKeyRepository.RevokeAsync(apiKeyId);
    }
}