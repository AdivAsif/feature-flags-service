using SharedKernel;

namespace Domain;

public sealed class AuditLog : EntityBase
{
    public Guid FeatureFlagId { get; set; } // foreign key to feature flag affected
    public AuditLogAction Action { get; set; } = AuditLogAction.Create;
    public string PerformedByUserId { get; set; } = string.Empty;
    public string PerformedByUserEmail { get; set; } = string.Empty;
    public string NewStateJson { get; set; } = string.Empty; // new state of feature flag
    public string PreviousStateJson { get; set; } = string.Empty; // previous state of feature flag, null if created
}

public enum AuditLogAction
{
    Create,
    Update,
    Delete
}