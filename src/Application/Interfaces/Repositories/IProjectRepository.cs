using Domain;

namespace Application.Interfaces.Repositories;

/// <summary>
///     Repository interface for Project operations.
/// </summary>
public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Project?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<Project>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default);
    Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}