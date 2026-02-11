using System.ComponentModel.DataAnnotations;
using SharedKernel;

namespace Domain;

public sealed class ApiKey : EntityBase
{
    public Guid ProjectId { get; set; }

    [MaxLength(64)] public string KeyHash { get; set; } = string.Empty;

    [MaxLength(20)] public string KeyPrefix { get; set; } = string.Empty;

    [MaxLength(100)] public string Name { get; set; } = string.Empty;

    [MaxLength(500)] public string Scopes { get; set; } = string.Empty;

    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    [MaxLength(100)] public string CreatedByUserId { get; set; } = string.Empty;

    public DateTimeOffset? RevokedAt { get; set; }
    public bool IsActive { get; set; } = true;
}