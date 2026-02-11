using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain;
using SharedKernel;

namespace Application.Services;

public sealed class EvaluationService(IRepository<FeatureFlag> featureFlagRepository) : IEvaluationService
{
    public async Task<EvaluationResultDTO> EvaluateAsync(string featureFlagKey, EvaluationContext context)
    {
        if (string.IsNullOrWhiteSpace(featureFlagKey))
            throw new BadRequestException("Feature flag key cannot be null or whitespace");

        if (context is null)
            throw new BadRequestException("Evaluation context cannot be null");

        if (string.IsNullOrWhiteSpace(context.UserId))
            throw new BadRequestException("User ID cannot be null or whitespace");

        var featureFlag = await featureFlagRepository.GetByKeyAsync(featureFlagKey);
        return EvaluationHelper(featureFlag, context);
    }

    private static EvaluationResultDTO EvaluationHelper(FeatureFlag? featureFlag, EvaluationContext context)
    {
        if (featureFlag is null)
            return new EvaluationResultDTO
            {
                Allowed = false,
                Reason = "Feature flag not found"
            };

        var defaultResult = new EvaluationResultDTO
        {
            Allowed = featureFlag.Enabled,
            Reason = featureFlag.Enabled ? "Feature flag is enabled" : "Feature flag is disabled"
        };

        var featureFlagRules = featureFlag.Parameters;
        if (featureFlagRules.Length == 0) return defaultResult;

        /*
         * There can be multiple rules defined for a feature flag. For users, it is as simple as checking whether the user matches;
         * the evaluation ends there.
         * For other rules, we need to evaluate them in order of priority. If it is by group, and also percentage,
         * i.e., the user is a beta tester, and 50% of users are allowed, then 50% of beta testers are allowed.
         */

        // Check the user first, as any of the rules contain the user, it evaluates immediately and is either allowed or denied
        // No further checks are needed - this is the highest priority rule
        if (featureFlagRules.Any(r => r.RuleType == RuleType.User) &&
            featureFlagRules.Any(r => r.RuleValue.Contains(context.UserId)))
            return new EvaluationResultDTO { Allowed = featureFlag.Enabled, Reason = "User is allowed" };

        if (featureFlagRules.Any(r => r.RuleType == RuleType.Percentage))
        {
            var hash = context.UserId.GetHashCode();
            var percentage = Math.Abs(hash % 100);
            var percentageRules = featureFlagRules.Where(r => r.RuleType == RuleType.Percentage);
            foreach (var rule in percentageRules)
                if (int.TryParse(rule.RuleValue, out var percentageValue) && percentage <= percentageValue)
                    return new EvaluationResultDTO
                        { Allowed = featureFlag.Enabled, Reason = "Percentage rule matched" };
                else
                    return new EvaluationResultDTO { Allowed = false, Reason = "Percentage rule did not match" };
        }

        return defaultResult;
    }
}