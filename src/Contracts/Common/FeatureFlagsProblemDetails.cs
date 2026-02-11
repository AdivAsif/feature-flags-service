using System.Text.Json.Serialization;

namespace Contracts.Common;

public sealed class FeatureFlagsProblemDetails
{
    public string? Type { get; init; }
    public string? Title { get; init; }
    public int? Status { get; init; }
    public string? Detail { get; init; }
    public string? Instance { get; init; }

    [JsonPropertyName("traceId")] public string? TraceId { get; init; }
}