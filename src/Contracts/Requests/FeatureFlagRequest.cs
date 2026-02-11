using Domain;

namespace Contracts.Requests;

public sealed record CreateFeatureFlagRequest(
    string Key,
    string Description = "",
    bool Enabled = true,
    FeatureFlagParameters[]? Parameters = null);

public sealed record UpdateFeatureFlagRequest(
    string Description,
    bool Enabled,
    FeatureFlagParameters[]? Parameters = null);