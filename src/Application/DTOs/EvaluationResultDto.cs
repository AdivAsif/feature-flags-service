namespace Application.DTOs;

public sealed record EvaluationResultDto(bool Allowed, string? Reason);