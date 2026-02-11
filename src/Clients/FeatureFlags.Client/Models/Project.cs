namespace FeatureFlags.Client.Models;

public sealed class Project : DtoBase
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}