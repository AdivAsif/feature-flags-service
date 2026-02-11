using SharedKernel;

namespace Application.DTOs;

public sealed class ApiKeyDto : DtoBase
{
    public Guid ProjectId { get; set; }
    public string KeyPrefix { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset? RevokedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class CreateApiKeyDTO
{
    public string Name { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class ApiKeyCreatedDTO
{
    public Guid Id { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}