using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Domain;
using Infrastructure.Repositories;

namespace Infrastructure.Services;

public sealed class ProjectService : IProjectService
{
    private readonly ProjectRepository _projectRepository;

    public ProjectService(ProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;
    }

    public async Task<ProjectDTO?> GetByIdAsync(Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
            throw new NotFoundException($"Project with id: {id} not found");

        return new ProjectDTO
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            IsActive = project.IsActive,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };
    }

    public async Task<IEnumerable<ProjectDTO>> GetAllAsync()
    {
        var projects = await _projectRepository.GetAllAsync();
        return projects.Select(p => new ProjectDTO
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            IsActive = p.IsActive,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        });
    }

    public async Task<ProjectDTO> CreateAsync(CreateProjectDTO dto, string? performedByUserId = null)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BadRequestException("Project name is required");

        var project = new Project
        {
            Name = dto.Name,
            Description = dto.Description,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var created = await _projectRepository.CreateAsync(project);

        return new ProjectDTO
        {
            Id = created.Id,
            Name = created.Name,
            Description = created.Description,
            IsActive = created.IsActive,
            CreatedAt = created.CreatedAt,
            UpdatedAt = created.UpdatedAt
        };
    }

    public async Task<ProjectDTO> UpdateAsync(Guid id, UpdateProjectDTO dto)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
            throw new NotFoundException($"Project with id: {id} not found");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BadRequestException("Project name is required");

        project.Name = dto.Name;
        project.Description = dto.Description;
        project.IsActive = dto.IsActive;

        var updated = await _projectRepository.UpdateAsync(project);

        return new ProjectDTO
        {
            Id = updated.Id,
            Name = updated.Name,
            Description = updated.Description,
            IsActive = updated.IsActive,
            CreatedAt = updated.CreatedAt,
            UpdatedAt = updated.UpdatedAt
        };
    }

    public async Task DeleteAsync(Guid id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null)
            throw new NotFoundException($"Project with id: {id} not found");

        await _projectRepository.DeleteAsync(id);
    }
}