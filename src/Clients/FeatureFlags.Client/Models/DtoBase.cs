namespace FeatureFlags.Client.Models;

public abstract class DtoBase
{
    public Guid Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}