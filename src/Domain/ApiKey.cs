using System.ComponentModel.DataAnnotations;

namespace Domain;

public sealed class ApiKey : EntityBase
{
    // Foreign key to its project, projects are essentially tenants right now - the primary unit of isolation
    public Guid ProjectId { get; init; }

    [MaxLength(64)] public string KeyHash { get; init; } = string.Empty;

    [MaxLength(20)] public string KeyPrefix { get; init; } = string.Empty;

    [MaxLength(100)] public string Name { get; init; } = string.Empty;

    [MaxLength(500)] public string Scopes { get; init; } = string.Empty; // flags:read flags:write flags:delete

    public DateTimeOffset? ExpiresAt { get; init; }

    // Updates in the background when the key is used - not critical to be perfectly up to date, so eventual consistency is fine
    public DateTimeOffset? LastUsedAt { get; init; }

    [MaxLength(100)] public string CreatedByUserId { get; init; } = string.Empty;

    public DateTimeOffset? RevokedAt { get; init; }
    public bool IsActive { get; init; } = true;
}