using Application.DTOs;
using Domain;

namespace Application.Interfaces;

public interface IEvaluationService
{
    Task<EvaluationResultDTO> EvaluateAsync(string featureFlagKey, EvaluationContext context);
}