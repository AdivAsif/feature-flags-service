using Application.Common;
using Application.Interfaces.Repositories;
using Application.Services;
using BenchmarkDotNet.Attributes;
using Contracts.Responses;
using Domain;
using EvaluationContext = Contracts.Models.EvaluationContext;

namespace FeatureFlagsService.Benchmarks;

[MemoryDiagnoser]
public class EvaluationBenchmarks
{
    private readonly Guid _projectId = Guid.NewGuid();
    private EvaluationContext _groupMatchContext = null!;
    private FeatureFlag _groupMatchFlag = null!;
    private EvaluationContext _groupMissContext = null!;
    private FeatureFlag _groupMissFlag = null!;
    private EvaluationContext _percentageContext = null!;
    private FeatureFlag _percentageFlag = null!;
    private EvaluationService _service = null!;
    private EvaluationContext _userMatchContext = null!;
    private FeatureFlag _userTargetFlag = null!;

    [GlobalSetup]
    public void Setup()
    {
        _userTargetFlag = new FeatureFlag
        {
            Key = "flag-user-target",
            Enabled = true,
            Parameters =
            [
                new FeatureFlagParameters
                {
                    RuleType = RuleType.User,
                    RuleValue = "user-123, user-456"
                }
            ]
        };

        _groupMatchFlag = new FeatureFlag
        {
            Key = "flag-group-match",
            Enabled = true,
            Parameters =
            [
                new FeatureFlagParameters
                {
                    RuleType = RuleType.Group,
                    RuleValue = "beta, staff"
                }
            ]
        };

        _groupMissFlag = new FeatureFlag
        {
            Key = "flag-group-miss",
            Enabled = true,
            Parameters =
            [
                new FeatureFlagParameters
                {
                    RuleType = RuleType.Group,
                    RuleValue = "beta, staff"
                }
            ]
        };

        _percentageFlag = new FeatureFlag
        {
            Key = "flag-percentage",
            Enabled = true,
            Parameters =
            [
                new FeatureFlagParameters
                {
                    RuleType = RuleType.Percentage,
                    RuleValue = "50"
                }
            ]
        };

        _userMatchContext = new EvaluationContext { UserId = "user-123" };
        _groupMatchContext = new EvaluationContext { UserId = "user-789", Groups = ["beta", "public"] };
        _groupMissContext = new EvaluationContext { UserId = "user-789", Groups = ["public", "general"] };
        _percentageContext = new EvaluationContext { UserId = "user-42" };

        var repository = new InMemoryFeatureFlagRepository(new Dictionary<string, FeatureFlag>
        {
            [_userTargetFlag.Key] = _userTargetFlag,
            [_groupMatchFlag.Key] = _groupMatchFlag,
            [_groupMissFlag.Key] = _groupMissFlag,
            [_percentageFlag.Key] = _percentageFlag
        });

        _service = new EvaluationService(repository);
    }

    [Benchmark]
    public Task<EvaluationResponse> Evaluate_UserTarget()
    {
        return _service.EvaluateAsync(_projectId, _userTargetFlag.Key, _userMatchContext);
    }

    [Benchmark]
    public Task<EvaluationResponse> Evaluate_GroupMatch()
    {
        return _service.EvaluateAsync(_projectId, _groupMatchFlag.Key, _groupMatchContext);
    }

    [Benchmark]
    public Task<EvaluationResponse> Evaluate_GroupMiss()
    {
        return _service.EvaluateAsync(_projectId, _groupMissFlag.Key, _groupMissContext);
    }

    [Benchmark]
    public Task<EvaluationResponse> Evaluate_Percentage()
    {
        return _service.EvaluateAsync(_projectId, _percentageFlag.Key, _percentageContext);
    }

    private sealed class InMemoryFeatureFlagRepository(IDictionary<string, FeatureFlag> flags)
        : IFeatureFlagRepository
    {
        public Task<FeatureFlag?> GetByIdAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FeatureFlag?> GetByKeyAsync(Guid projectId, string key,
            CancellationToken cancellationToken = default)
        {
            flags.TryGetValue(key, out var flag);
            return Task.FromResult(flag);
        }

        public Task<Slice<FeatureFlag>> GetPagedAsync(Guid projectId, int first = 10, string? after = null,
            string? before = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FeatureFlag> CreateAsync(FeatureFlag featureFlag, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<FeatureFlag> UpdateAsync(FeatureFlag featureFlag, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}