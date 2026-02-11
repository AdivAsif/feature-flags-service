using Contracts.Common;
using Domain;

namespace Contracts.Responses;

public sealed class FeatureFlagResponse : ContractBase
{
    public Guid ProjectId { get; init; }
    public int Version { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public FeatureFlagParameters[] Parameters { get; init; } = [];
}