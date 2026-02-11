using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Caching;
using ZiggyCreatures.Caching.Fusion;

namespace Infrastructure.Repositories;

/// <summary>
///     Cached decorator for ProjectRepository using FusionCache.
///     Cache Strategy:
///     - Local: 2 hours (projects change infrequently)
///     - Distributed: 12 hours
///     - Background operations enabled
/// </summary>
public class CachedProjectRepository(IProjectRepository innerRepository, IFusionCache cache) : IProjectRepository
{
    private static readonly FusionCacheEntryOptions CacheOptions = new()
    {
        Duration = TimeSpan.FromHours(2),
        DistributedCacheDuration = TimeSpan.FromHours(12),
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromHours(24),
        AllowBackgroundDistributedCacheOperations = true,
        Size = 1
    };

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.Project(id);

        return await cache.GetOrSetAsync(
            cacheKey,
            async _ => await innerRepository.GetByIdAsync(id, cancellationToken),
            CacheOptions,
            cancellationToken);
    }

    public async Task<IEnumerable<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.AllProjects();

        return await cache.GetOrSetAsync(
            cacheKey,
            async _ => await innerRepository.GetAllAsync(cancellationToken),
            new FusionCacheEntryOptions
            {
                Duration = TimeSpan.FromMinutes(30),
                DistributedCacheDuration = TimeSpan.FromHours(6),
                AllowBackgroundDistributedCacheOperations = true,
                Size = 1
            },
            cancellationToken);
    }

    public async Task<Project?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.ProjectByName(name);

        return await cache.GetOrSetAsync(
            cacheKey,
            async _ => await innerRepository.GetByNameAsync(name, cancellationToken),
            CacheOptions,
            cancellationToken);
    }

    public async Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default)
    {
        var created = await innerRepository.CreateAsync(project, cancellationToken);

        await cache.SetAsync(CacheKeys.Project(created.Id), created, CacheOptions, cancellationToken);
        await cache.SetAsync(CacheKeys.ProjectByName(created.Name), created, CacheOptions, cancellationToken);
        await cache.RemoveAsync(CacheKeys.AllProjects(), token: cancellationToken);

        return created;
    }

    public async Task<Project> UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        var existing = await innerRepository.GetByIdAsync(project.Id, cancellationToken);
        var previousName = existing?.Name;

        var updated = await innerRepository.UpdateAsync(project, cancellationToken);

        await cache.SetAsync(CacheKeys.Project(updated.Id), updated, CacheOptions, cancellationToken);
        await cache.SetAsync(CacheKeys.ProjectByName(updated.Name), updated, CacheOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousName) &&
            !string.Equals(previousName, updated.Name, StringComparison.Ordinal))
            await cache.RemoveAsync(CacheKeys.ProjectByName(previousName), token: cancellationToken);

        await cache.RemoveAsync(CacheKeys.AllProjects(), token: cancellationToken);

        return updated;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await innerRepository.GetByIdAsync(id, cancellationToken);

        await innerRepository.DeleteAsync(id, cancellationToken);

        await cache.RemoveAsync(CacheKeys.Project(id), token: cancellationToken);
        if (existing != null)
            await cache.RemoveAsync(CacheKeys.ProjectByName(existing.Name), token: cancellationToken);
        await cache.RemoveAsync(CacheKeys.AllProjects(), token: cancellationToken);
    }
}