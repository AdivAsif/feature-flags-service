using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class ProjectRepository(IDbContextFactory<FeatureFlagsDbContext> contextFactory)
    : BaseRepository<FeatureFlagsDbContext>(contextFactory), IProjectRepository
{
    // GET
    public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            db => db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken),
            cancellationToken);
    }

    public Task<Project?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            db => db.Projects.AsNoTracking()
                .Where(p => p.IsActive && p.Name == name)
                .FirstOrDefaultAsync(cancellationToken),
            cancellationToken);
    }

    public async Task<IEnumerable<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(db => db.Projects
            .AsNoTracking()
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken), cancellationToken);
    }

    // CREATE
    public Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async db =>
        {
            await db.Projects.AddAsync(project, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return project;
        }, cancellationToken);
    }

    // UPDATE
    public Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async db =>
        {
            db.Entry(project).State = EntityState.Modified;
            await db.SaveChangesAsync(cancellationToken);
            return project;
        }, cancellationToken);
    }

    // DELETE (soft-delete)
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async db =>
            await db.Projects
                .Where(p => p.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.IsActive, false)
                    .SetProperty(p => p.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken), cancellationToken);
    }
}