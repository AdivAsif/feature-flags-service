using Application.DTOs;

namespace Application.Interfaces;

public interface IProjectService
{
    Task<ProjectDTO?> GetByIdAsync(Guid id);
    Task<IEnumerable<ProjectDTO>> GetAllAsync();
    Task<ProjectDTO> CreateAsync(CreateProjectDTO dto, string? performedByUserId = null);
    Task<ProjectDTO> UpdateAsync(Guid id, UpdateProjectDTO dto);
    Task DeleteAsync(Guid id);
}