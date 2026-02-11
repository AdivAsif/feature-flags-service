namespace Application.DTOs;

public sealed class EvaluationResultDTO
{
    public bool Allowed { get; set; } = false;
    public string? Reason { get; set; } = string.Empty;
}