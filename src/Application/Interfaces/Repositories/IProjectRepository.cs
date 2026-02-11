using Domain;

namespace Application.Interfaces.Repositories;

// Repository interface for Project persistence
// Can be decorated with caching using Scrutor, implemented in the Infrastructure layer
public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Project?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IEnumerable<Project>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default);
    Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}