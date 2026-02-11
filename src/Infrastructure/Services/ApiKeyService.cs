using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Authentication;

namespace Infrastructure.Services;

public sealed class ApiKeyService : IApiKeyService
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IProjectRepository _projectRepository;

    public ApiKeyService(IApiKeyRepository apiKeyRepository, IProjectRepository projectRepository)
    {
        _apiKeyRepository = apiKeyRepository;
        _projectRepository = projectRepository;
    }

    public async Task<ApiKeyCreatedDTO> CreateAsync(Guid projectId, CreateApiKeyDTO dto, string createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
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

        var created = await _apiKeyRepository.CreateAsync(apiKeyEntity, cancellationToken);

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

    public async Task<IEnumerable<ApiKeyDto>> GetByProjectIdAsync(Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project == null)
            throw new NotFoundException($"Project with id: {projectId} not found");

        var apiKeys = await _apiKeyRepository.GetByProjectIdAsync(projectId, cancellationToken);

        return apiKeys.Select(k => new ApiKeyDto
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

    public async Task RevokeAsync(Guid projectId, Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken);
        if (project == null)
            throw new NotFoundException($"Project with id: {projectId} not found");

        // Note: We could add validation here to ensure the API key belongs to the project
        await _apiKeyRepository.RevokeAsync(apiKeyId, cancellationToken);
    }
}