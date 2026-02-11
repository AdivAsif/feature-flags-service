namespace Domain;

public sealed record EvaluationContext(
    string UserId,
    string Email,
    IEnumerable<string> Groups,
    string? TenantId,
    string? Environment);