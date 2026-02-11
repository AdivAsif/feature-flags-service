using Domain;
using Riok.Mapperly.Abstractions;
using SharedKernel;

namespace Application.DTOs;

public sealed class FeatureFlagDto : DtoBase
{
    public Guid ProjectId { get; set; }
    public int Version { get; set; } = 1;
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public FeatureFlagParameters[] Parameters { get; set; } = [];
}

[Mapper]
public partial class FeatureFlagMapper
{
    // Get
    public partial FeatureFlagDto FeatureFlagToFeatureFlagDto(FeatureFlag featureFlag);
    public partial IEnumerable<FeatureFlagDto> FeatureFlagsToFeatureFlagDtos(IEnumerable<FeatureFlag> featureFlags);

    // Create
    public partial FeatureFlag FeatureFlagDtoToFeatureFlag(FeatureFlagDto dto);

    // Update
    public partial void FeatureFlagDtoToFeatureFlag(FeatureFlagDto dto, [MappingTarget] FeatureFlag entity);
}