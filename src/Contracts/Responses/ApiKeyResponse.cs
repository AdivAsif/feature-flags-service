using Contracts.Common;

namespace Contracts.Responses;

public sealed class ApiKeyResponse : ContractBase
{
    public Guid ProjectId { get; init; }
    public string KeyPrefix { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Scopes { get; init; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
    public string CreatedByUserId { get; init; } = string.Empty;
    public DateTimeOffset? RevokedAt { get; init; }
    public bool IsActive { get; init; }
}

public sealed record ApiKeyCreatedResponse(
    Guid Id,
    string ApiKey,
    string Name,
    string Scopes,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);