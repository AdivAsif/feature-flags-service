using Application.DTOs;

namespace Application.Interfaces;

public interface IProjectService
{
    Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProjectDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CreateProjectResult> CreateAsync(CreateProjectDto dto, string? performedByUserId = null,
        CancellationToken cancellationToken = default);

    Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}