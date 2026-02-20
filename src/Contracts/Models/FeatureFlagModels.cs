namespace Contracts.Models;

public readonly struct EvaluationContext()
{
    public string UserId { get; init; } = "anonymous";

    public IReadOnlyList<string>? Groups { get; init; } = null;
    // public string? Email { get; init; }
    // public string? TenantId { get; init; }
    // public string? Environment { get; init; }
}