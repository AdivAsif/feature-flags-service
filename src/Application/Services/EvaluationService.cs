using System.IO.Hashing;
using System.Text;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Contracts.Responses;
using Domain;
using EvaluationContext = Contracts.Models.EvaluationContext;

namespace Application.Services;

public sealed class EvaluationService(IFeatureFlagRepository featureFlagRepository) : IEvaluationService
{
    public async Task<EvaluationResponse> EvaluateAsync(Guid projectId, string featureFlagKey,
        EvaluationContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(featureFlagKey))
            throw new BadRequestException("Feature flag key cannot be null or whitespace");

        if (string.IsNullOrWhiteSpace(context.UserId))
            throw new BadRequestException("User ID cannot be null or whitespace");

        var featureFlag = await featureFlagRepository.GetByKeyAsync(projectId, featureFlagKey, cancellationToken);

        return await EvaluateAsync(featureFlag, context, cancellationToken);
    }

    public Task<EvaluationResponse> EvaluateAsync(FeatureFlag? featureFlag, EvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(EvaluationHelper(featureFlag, context));
    }

    private static EvaluationResponse EvaluationHelper(FeatureFlag? featureFlag, EvaluationContext context)
    {
        if (featureFlag is null)
            return new EvaluationResponse { Allowed = false, Reason = "Feature flag not found" };

        var defaultResult = new EvaluationResponse
        {
            Allowed = featureFlag.Enabled,
            Reason = featureFlag.Enabled ? "Feature flag is enabled" : "Feature flag is disabled"
        };

        var rules = featureFlag.Parameters;
        if (rules.Length == 0) return defaultResult;

        for (var i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];
            if (rule.RuleType != RuleType.User) continue;
            if (IsValueInCommaSeparatedList(rule.RuleValue, context.UserId))
                return new EvaluationResponse { Allowed = featureFlag.Enabled, Reason = "User is explicitly targeted" };
        }

        // 2. Check if user belongs to any required groups
        var userGroups = context.Groups;
        var groupRuleExists = false;
        string? matchedGroup = null;

        for (var i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];
            if (rule.RuleType != RuleType.Group) continue;
            groupRuleExists = true;
            if (userGroups != null)
                foreach (var userGroup in userGroups)
                    if (IsValueInCommaSeparatedList(rule.RuleValue, userGroup))
                    {
                        matchedGroup = userGroup;
                        break;
                    }

            if (matchedGroup != null) break;
        }

        if (groupRuleExists && matchedGroup == null)
            return new EvaluationResponse { Allowed = false, Reason = "User not in required group" };

        // 3. Percentage rollout
        for (var i = 0; i < rules.Length; i++)
        {
            var rule = rules[i];
            if (rule.RuleType != RuleType.Percentage) continue;
            if (!int.TryParse(rule.RuleValue, out var percentageValue)) continue;
            var bucket = CalculateBucket(context.UserId, featureFlag.Key);
            if (bucket < percentageValue)
            {
                var reason = matchedGroup != null
                    ? $"User in '{matchedGroup}' group and within {percentageValue}% rollout"
                    : $"User within {percentageValue}% rollout";
                return new EvaluationResponse { Allowed = featureFlag.Enabled, Reason = reason };
            }
            else
            {
                var reason = matchedGroup != null
                    ? $"User in '{matchedGroup}' group but outside {percentageValue}% rollout"
                    : $"User outside {percentageValue}% rollout";
                return new EvaluationResponse { Allowed = false, Reason = reason };
            }
        }

        // 4. If user is in required group but no percentage rule, allow
        return matchedGroup != null
            ? new EvaluationResponse
                { Allowed = featureFlag.Enabled, Reason = $"User in required group '{matchedGroup}'" }
            : defaultResult;
    }

    private static bool IsValueInCommaSeparatedList(string list, string value)
    {
        if (string.IsNullOrEmpty(list)) return false;

        var span = list.AsSpan();
        while (span.Length > 0)
        {
            var commaIndex = span.IndexOf(',');
            ReadOnlySpan<char> item;
            if (commaIndex == -1)
            {
                item = span;
                span = ReadOnlySpan<char>.Empty;
            }
            else
            {
                item = span[..commaIndex];
                span = span[(commaIndex + 1)..];
            }

            item = item.Trim();
            if (item.Equals(value.AsSpan(), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static int CalculateBucket(string userId, string flagKey)
    {
        // Optimized consistent hashing using XxHash32
        // Much faster than SHA256 while maintaining good distribution
        // stackalloc to avoid string allocation for the combined key similar to group hashing
        // This bucket implementation is similar to LaunchDarkly's, but simplified and optimized for this context
        var totalLen = flagKey.Length + 1 + userId.Length;
        uint hash;
        if (totalLen <= 256)
        {
            Span<char> charBuffer = stackalloc char[totalLen];
            flagKey.AsSpan().CopyTo(charBuffer);
            charBuffer[flagKey.Length] = '.';
            userId.AsSpan().CopyTo(charBuffer[(flagKey.Length + 1)..]);

            Span<byte> byteBuffer = stackalloc byte[Encoding.UTF8.GetMaxByteCount(totalLen)];
            var bytesWritten = Encoding.UTF8.GetBytes(charBuffer, byteBuffer);
            hash = XxHash32.HashToUInt32(byteBuffer[..bytesWritten]);
        }
        else
        {
            var input = $"{flagKey}.{userId}";
            var bytes = Encoding.UTF8.GetBytes(input);
            hash = XxHash32.HashToUInt32(bytes);
        }

        return (int)(hash % 100);
    }
}