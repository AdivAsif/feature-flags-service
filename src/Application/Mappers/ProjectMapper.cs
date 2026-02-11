using Contracts.Requests;
using Contracts.Responses;
using Domain;
using Riok.Mapperly.Abstractions;

namespace Application.Mappers;

[Mapper]
public partial class ProjectMapper
{
    public partial ProjectResponse ProjectToResponse(Project project);
    public partial IEnumerable<ProjectResponse> ProjectsToResponses(IEnumerable<Project> projects);

    public partial Project CreateRequestToProject(CreateProjectRequest request);

    public partial void UpdateRequestToProject(UpdateProjectRequest request, [MappingTarget] Project entity);
}