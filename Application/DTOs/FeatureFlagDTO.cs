using Domain;
using Riok.Mapperly.Abstractions;

namespace Application.DTOs;

public sealed class FeatureFlagDTO
{
    public Guid Id { get; set; } = Guid.Empty;
    public int Version { get; set; } = 1;
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public FeatureFlagParameters[] Parameters { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

[Mapper]
public partial class FeatureFlagMapper
{
    // Get
    public partial FeatureFlagDTO FeatureFlagToFeatureFlagDto(FeatureFlag featureFlag);
    public partial IEnumerable<FeatureFlagDTO> FeatureFlagsToFeatureFlagDtos(IEnumerable<FeatureFlag> featureFlags);

    // Create
    public partial FeatureFlag FeatureFlagDtoToFeatureFlag(FeatureFlagDTO dto);

    // Update
    public partial void FeatureFlagDtoToFeatureFlag(FeatureFlagDTO dto, [MappingTarget] FeatureFlag entity);
}