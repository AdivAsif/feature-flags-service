using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace FeatureFlagsService.Benchmarks;

[MemoryDiagnoser]
public class HashingBenchmarks
{
    private byte[] _combinedBytes = Array.Empty<byte>();
    private string _flagKey = string.Empty;

    private string _userId = string.Empty;

    [Params(32, 128, 512)] public int UserIdLength { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _flagKey = "new-ui";
        _userId = new string('u', UserIdLength);
        _combinedBytes = Encoding.UTF8.GetBytes($"{_flagKey}.{_userId}");
    }

    [Benchmark(Baseline = true)]
    public int XxHash32_Bucket()
    {
        return CalculateBucketXxHash32(_userId, _flagKey);
    }

    [Benchmark]
    public int XxHash64_Bucket()
    {
        return CalculateBucketXxHash64(_userId, _flagKey);
    }

    [Benchmark]
    public int Sha256_Bucket()
    {
        return CalculateBucketSha256(_combinedBytes);
    }

    private static int CalculateBucketXxHash32(string userId, string flagKey)
    {
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

    private static int CalculateBucketXxHash64(string userId, string flagKey)
    {
        var totalLen = flagKey.Length + 1 + userId.Length;
        ulong hash;
        if (totalLen <= 256)
        {
            Span<char> charBuffer = stackalloc char[totalLen];
            flagKey.AsSpan().CopyTo(charBuffer);
            charBuffer[flagKey.Length] = '.';
            userId.AsSpan().CopyTo(charBuffer[(flagKey.Length + 1)..]);

            Span<byte> byteBuffer = stackalloc byte[Encoding.UTF8.GetMaxByteCount(totalLen)];
            var bytesWritten = Encoding.UTF8.GetBytes(charBuffer, byteBuffer);
            hash = XxHash64.HashToUInt64(byteBuffer[..bytesWritten]);
        }
        else
        {
            var input = $"{flagKey}.{userId}";
            var bytes = Encoding.UTF8.GetBytes(input);
            hash = XxHash64.HashToUInt64(bytes);
        }

        return (int)(hash % 100);
    }

    private static int CalculateBucketSha256(byte[] inputBytes)
    {
        var hash = SHA256.HashData(inputBytes);
        var value = BitConverter.ToUInt32(hash, 0);
        return (int)(value % 100);
    }
}