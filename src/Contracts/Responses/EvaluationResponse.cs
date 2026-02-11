namespace Contracts.Responses;

public sealed record EvaluationResponse
{
    public bool Allowed { get; init; }
    public string? Reason { get; init; }
}