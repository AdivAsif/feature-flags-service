using System.ComponentModel.DataAnnotations;
using SharedKernel;

namespace Domain;

public sealed class FeatureFlag : EntityBase, IHasKey
{
    public int Version { get; set; } = 1; // version is an integer for optimistic concurrency control (OCC)
    [MaxLength(255)] public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } // global killswitch
    public FeatureFlagParameters[] Parameters { get; set; } = []; // parameters to determine who gets access to what
    [MaxLength(100)] public string Key { get; set; } = string.Empty;
}

public sealed class FeatureFlagParameters
{
    public RuleType RuleType { get; set; } = 0;

    [MaxLength(255)]
    public string RuleValue { get; set; } = string.Empty; // if there are multiple, comma-separated values will be used
}

public enum RuleType
{
    Percentage = 0, // i.e., 50% chance enabled - user attribute hashing to determine who is enabled
    Group = 1, // i.e., group A enabled, rest of the groups disabled (beta testers and such)
    User = 2, // i.e., specific user enabled
    Tenant = 3, // i.e., tenant A enabled, rest of the tenants disabled (enterprise customers and such)
    Environment = 4 // i.e., dev, test, prod
}