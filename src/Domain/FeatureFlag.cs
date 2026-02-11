using System.ComponentModel.DataAnnotations;

namespace Domain;

public sealed class FeatureFlag : EntityBase
{
    public int Version { get; set; } = 1; // Version is an integer for optimistic concurrency control (OCC)
    [MaxLength(255)] public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } // Global kill-switch

    public FeatureFlagParameters[] Parameters { get; set; } =
        []; // Parameters to determine who gets access to what and how

    [MaxLength(100)] public string Key { get; set; } = string.Empty; // Its identifier, unique per project

    public Guid
        ProjectId
    {
        get;
        set;
    } // Foreign key to its project, projects are essentially tenants right now - the primary unit of isolation
}

public sealed record FeatureFlagParameters
{
    public RuleType RuleType { get; set; } = 0;

    [MaxLength(255)]
    public string RuleValue { get; set; } = string.Empty; // If there are multiple, comma-separated values will be used
}

public enum RuleType
{
    Percentage = 0, // i.e., 50% chance enabled - user attribute hashing put in buckets to determine who is enabled
    Group = 1, // i.e., group A enabled, rest of the groups disabled (beta testers and such)

    User = 2 // i.e., specific user enabled
    // Tenant = 3, // i.e., tenant A enabled, rest of the tenants disabled (enterprise customers and such)
    // Environment = 4 // i.e., dev, test, prod
}