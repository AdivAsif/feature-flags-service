using Contracts.Requests;
using Contracts.Responses;

namespace Application.Interfaces;

public interface IProjectService
{
    Task<ProjectResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProjectResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<(ProjectResponse Project, bool Created)> CreateAsync(CreateProjectRequest dto,
        string? performedByUserId = null,
        CancellationToken cancellationToken = default);

    Task<ProjectResponse> UpdateAsync(Guid id, UpdateProjectRequest dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}