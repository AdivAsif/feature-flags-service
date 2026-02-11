using Application.DTOs;
using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Domain;

namespace Infrastructure.Services;

public sealed class ProjectService(IProjectRepository projectRepository) : IProjectService
{
    public async Task<ProjectDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(id, cancellationToken);
        return project == null ? throw new NotFoundException($"Project with id: {id} not found") : Map(project);
    }

    public async Task<IEnumerable<ProjectDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var projects = await projectRepository.GetAllAsync(cancellationToken);
        return projects.Select(Map);
    }

    public async Task<CreateProjectResult> CreateAsync(CreateProjectDto dto, string? performedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BadRequestException("Project name is required");

        var trimmedName = dto.Name.Trim();
        var existing = await projectRepository.GetByNameAsync(trimmedName, cancellationToken);
        if (existing != null)
            return new CreateProjectResult(Map(existing), false);

        var project = new Project
        {
            Name = trimmedName,
            Description = dto.Description,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var created = await projectRepository.CreateAsync(project, cancellationToken);

        return new CreateProjectResult(Map(created), true);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectDto dto,
        CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(id, cancellationToken);
        if (project == null)
            throw new NotFoundException($"Project with id: {id} not found");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BadRequestException("Project name is required");

        project.Name = dto.Name;
        project.Description = dto.Description;
        project.IsActive = dto.IsActive;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await projectRepository.UpdateAsync(project, cancellationToken);

        return Map(updated);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(id, cancellationToken);
        if (project == null)
            throw new NotFoundException($"Project with id: {id} not found");

        await projectRepository.DeleteAsync(id, cancellationToken);
    }

    private static ProjectDto Map(Project project)
    {
        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            IsActive = project.IsActive,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };
    }
}