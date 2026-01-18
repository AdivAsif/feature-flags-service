using SharedKernel;

namespace Domain;

public sealed class AuditLog : EntityBase
{
    public Guid FeatureFlagId { get; set; } // foreign key to feature flag affected
    public string Action { get; set; } = string.Empty; // create, update, delete
    public string PerformedByUserId { get; set; } = string.Empty;
    public string PerformedByUserEmail { get; set; } = string.Empty;
    public FeatureFlag NewFeatureFlagState { get; set; } = new(); // new state of feature flag

    public FeatureFlag? PreviousFeatureFlagState { get; set; } =
        new(); // previous state of feature flag, null if created
}