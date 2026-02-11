using System.Security.Cryptography;
using System.Text;
using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain;
using SharedKernel;

namespace Application.Services;

public sealed class EvaluationService(IKeyedRepository<FeatureFlag> featureFlagRepository) : IEvaluationService
{
    public async Task<EvaluationResultDTO> EvaluateAsync(Guid projectId, string featureFlagKey,
        EvaluationContext context)
    {
        if (string.IsNullOrWhiteSpace(featureFlagKey))
            throw new BadRequestException("Feature flag key cannot be null or whitespace");

        if (context is null)
            throw new BadRequestException("Evaluation context cannot be null");

        if (string.IsNullOrWhiteSpace(context.UserId))
            throw new BadRequestException("User ID cannot be null or whitespace");

        var featureFlag = await featureFlagRepository.GetByKeyAsync(projectId, featureFlagKey);

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
         * Rule evaluation order (priority):
         * 1. User targeting - Highest priority, immediate allow/deny
         * 2. Group + Percentage combination - e.g., 50% of beta testers
         * 3. Group targeting - User must be in specified group(s)
         * 4. Percentage rollout - Consistent hashing based bucketing
         */

        // 1. User targeting - highest priority
        var userRules = featureFlagRules.Where(r => r.RuleType == RuleType.User).ToArray();
        if (userRules.Length > 0)
        {
            var userIds = userRules.SelectMany(r =>
                r.RuleValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (userIds.Contains(context.UserId))
                return new EvaluationResultDTO
                    { Allowed = featureFlag.Enabled, Reason = "User is explicitly targeted" };
        }

        // 2. Check if user belongs to any required groups
        var groupRules = featureFlagRules.Where(r => r.RuleType == RuleType.Group).ToArray();
        var userGroups = context.Groups?.ToArray() ?? [];

        var userInRequiredGroup = false;
        string? matchedGroup = null;

        if (groupRules.Length > 0)
        {
            var requiredGroups = groupRules.SelectMany(r =>
                    r.RuleValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToArray();
            matchedGroup = userGroups.FirstOrDefault(ug => requiredGroups.Contains(ug));
            userInRequiredGroup = matchedGroup != null;

            // If groups are defined but user not in any, deny access
            if (!userInRequiredGroup)
                return new EvaluationResultDTO { Allowed = false, Reason = "User not in required group" };
        }

        // 3. Percentage rollout (with optional group scoping)
        var percentageRules = featureFlagRules.Where(r => r.RuleType == RuleType.Percentage).ToArray();
        if (percentageRules.Length > 0)
        {
            // Use consistent hashing similar to LaunchDarkly
            var bucket = CalculateBucket(context.UserId, featureFlag.Key);

            foreach (var rule in percentageRules)
                if (int.TryParse(rule.RuleValue, out var percentageValue))
                {
                    if (bucket < percentageValue)
                    {
                        var reason = userInRequiredGroup
                            ? $"User in '{matchedGroup}' group and within {percentageValue}% rollout"
                            : $"User within {percentageValue}% rollout";
                        return new EvaluationResultDTO { Allowed = featureFlag.Enabled, Reason = reason };
                    }
                    else
                    {
                        var reason = userInRequiredGroup
                            ? $"User in '{matchedGroup}' group but outside {percentageValue}% rollout"
                            : $"User outside {percentageValue}% rollout";
                        return new EvaluationResultDTO { Allowed = false, Reason = reason };
                    }
                }
        }

        // 4. If user is in required group but no percentage rule, allow
        if (userInRequiredGroup)
            return new EvaluationResultDTO
                { Allowed = featureFlag.Enabled, Reason = $"User in required group '{matchedGroup}'" };

        return defaultResult;
    }

    private static int CalculateBucket(string userId, string flagKey)
    {
        // Consistent hashing similar to LaunchDarkly's bucket algorithm
        // Combines userId + flagKey to ensure same user gets different buckets for different flags
        var input = $"{flagKey}.{userId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));

        // Take first 4 bytes and convert to int, then mod 100 for 0-99 range
        var intHash = BitConverter.ToUInt32(hash, 0);
        return (int)(intHash % 100);
    }
}