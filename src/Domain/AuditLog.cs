namespace Domain;

// Eventually consistent, append-only log of changes to feature flags for auditing and debugging purposes
public sealed class AuditLog : EntityBase
{
    public Guid FeatureFlagId { get; set; } // Foreign key to a feature flag affected
    public AuditLogAction Action { get; set; } = AuditLogAction.Create;
    public string PerformedByUserId { get; set; } = string.Empty; // Taken from a JWT token if used, otherwise API Key
    public string PerformedByUserEmail { get; set; } = string.Empty;
    public string NewStateJson { get; set; } = string.Empty; // New state of a feature flag
    public string PreviousStateJson { get; set; } = string.Empty; // Previous state of a feature flag, null if created
}

public enum AuditLogAction
{
    Create,
    Update,
    Delete
}