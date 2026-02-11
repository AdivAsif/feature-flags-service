using System.ComponentModel.DataAnnotations;
using SharedKernel;

namespace Domain;

public sealed class ApiKey : EntityBase
{
    public Guid ProjectId { get; init; }

    [MaxLength(64)] public string KeyHash { get; init; } = string.Empty;

    [MaxLength(20)] public string KeyPrefix { get; init; } = string.Empty;

    [MaxLength(100)] public string Name { get; init; } = string.Empty;

    [MaxLength(500)] public string Scopes { get; init; } = string.Empty;

    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }

    [MaxLength(100)] public string CreatedByUserId { get; init; } = string.Empty;

    public DateTimeOffset? RevokedAt { get; init; }
    public bool IsActive { get; init; } = true;
}