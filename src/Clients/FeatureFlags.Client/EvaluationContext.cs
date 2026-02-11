namespace FeatureFlags.Client;

public sealed class EvaluationContext
{
    public string UserId { get; init; } = "anonymous";
    public string? Email { get; init; }
    public IReadOnlyList<string>? Groups { get; init; }
    public string? TenantId { get; init; }
    public string? Environment { get; init; }
}