using BenchmarkDotNet.Attributes;
using Infrastructure.Caching;

namespace FeatureFlagsService.Benchmarks;

[MemoryDiagnoser]
public class ContextHashBenchmarks
{
    private string[] _groups = Array.Empty<string>();

    [Params(0, 3, 10, 30)] public int GroupCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _groups = Enumerable.Range(0, GroupCount)
            .Select(i => $"group-{GroupCount - i:000}")
            .ToArray();
    }

    [Benchmark]
    public ulong HashContext()
    {
        return CacheKeys.HashContext(_groups);
    }
}