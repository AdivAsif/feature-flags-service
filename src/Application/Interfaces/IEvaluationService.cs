using Application.DTOs;
using Domain;

namespace Application.Interfaces;

public interface IEvaluationService
{
    Task<EvaluationResultDTO> EvaluateAsync(Guid projectId, string featureFlagKey, EvaluationContext context);
}