using Application.Exceptions;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Contracts.Requests;
using Contracts.Responses;
using Domain;

namespace Infrastructure.Services;

public sealed class ProjectService(IProjectRepository projectRepository) : IProjectService
{
    public async Task<ProjectResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await projectRepository.GetByIdAsync(id, cancellationToken);
        return project == null ? throw new NotFoundException($"Project with id: {id} not found") : Map(project);
    }

    public async Task<IEnumerable<ProjectResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var projects = await projectRepository.GetAllAsync(cancellationToken);
        return projects.Select(Map);
    }

    public async Task<(ProjectResponse Project, bool Created)> CreateAsync(CreateProjectRequest dto,
        string? performedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BadRequestException("Project name is required");

        var trimmedName = dto.Name.Trim();
        var existing = await projectRepository.GetByNameAsync(trimmedName, cancellationToken);
        if (existing != null)
            return (Map(existing), false);

        var project = new Project
        {
            Name = trimmedName,
            Description = dto.Description,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var created = await projectRepository.CreateAsync(project, cancellationToken);

        return (Map(created), true);
    }

    public async Task<ProjectResponse> UpdateAsync(Guid id, UpdateProjectRequest dto,
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

    private static ProjectResponse Map(Project project)
    {
        return new ProjectResponse
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