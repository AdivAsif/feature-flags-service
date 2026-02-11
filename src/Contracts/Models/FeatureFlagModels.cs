namespace Contracts.Models;

public sealed record EvaluationContext
{
    public string UserId { get; init; } = "anonymous";

    public IReadOnlyList<string>? Groups { get; init; }
    // public string? Email { get; init; }
    // public string? TenantId { get; init; }
    // public string? Environment { get; init; }
}