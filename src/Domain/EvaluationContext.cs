namespace Domain;

public sealed record EvaluationContext(
    string UserId,
    IEnumerable<string> Groups);