namespace FeatureFlags.Client;

public sealed class EvaluationResult
{
    public bool Allowed { get; init; }
    public string? Reason { get; init; }
}