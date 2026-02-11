using System.Text.Json;

namespace FeatureFlags.Client.Models;

public sealed class FeatureFlag : DtoBase
{
    public Guid ProjectId { get; init; }
    public int Version { get; init; } = 1;
    public string Key { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool Enabled { get; init; }

    public JsonElement[] Parameters { get; init; } = [];
}