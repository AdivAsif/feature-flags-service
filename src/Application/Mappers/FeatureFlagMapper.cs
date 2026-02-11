using Contracts.Requests;
using Contracts.Responses;
using Domain;
using Riok.Mapperly.Abstractions;

namespace Application.Mappers;

[Mapper]
public partial class FeatureFlagMapper
{
    public partial FeatureFlagResponse FeatureFlagToResponse(FeatureFlag entity);
    public partial IEnumerable<FeatureFlagResponse> FeatureFlagsToResponses(IEnumerable<FeatureFlag> entities);

    [MapperIgnoreTarget(nameof(FeatureFlag.Id))]
    [MapperIgnoreTarget(nameof(FeatureFlag.Version))]
    [MapperIgnoreTarget(nameof(FeatureFlag.ProjectId))]
    [MapperIgnoreTarget(nameof(FeatureFlag.CreatedAt))]
    [MapperIgnoreTarget(nameof(FeatureFlag.UpdatedAt))]
    public partial FeatureFlag CreateRequestToEntity(CreateFeatureFlagRequest request);

    [MapperIgnoreTarget(nameof(FeatureFlag.Id))]
    [MapperIgnoreTarget(nameof(FeatureFlag.Key))]
    [MapperIgnoreTarget(nameof(FeatureFlag.Version))]
    [MapperIgnoreTarget(nameof(FeatureFlag.ProjectId))]
    [MapperIgnoreTarget(nameof(FeatureFlag.CreatedAt))]
    [MapperIgnoreTarget(nameof(FeatureFlag.UpdatedAt))]
    public partial void UpdateRequestToEntity(UpdateFeatureFlagRequest request, [MappingTarget] FeatureFlag entity);
}