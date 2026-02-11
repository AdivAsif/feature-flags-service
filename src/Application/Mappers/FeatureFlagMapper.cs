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

    public partial FeatureFlag CreateRequestToEntity(CreateFeatureFlagRequest request);

    public partial void UpdateRequestToEntity(UpdateFeatureFlagRequest request, [MappingTarget] FeatureFlag entity);
}