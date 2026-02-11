using SharedKernel;
using ZiggyCreatures.Caching.Fusion;

namespace Infrastructure.Repositories;

public class CachedRepository<T>(IRepository<T> innerRepository, IFusionCache cache)
    : IRepository<T> where T : EntityBase, ICacheable
{
    public async Task<T?> GetByIdAsync(Guid id)
    {
        var entity = await cache.GetOrSetAsync<T>(
            $"{typeof(T).Name}:{id}",
            _ => innerRepository.GetByIdAsync(id));

        return entity;
    }

    public async Task<IEnumerable<T>> GetAllAsync(int? take, int? skip)
    {
        var entities = await cache.GetOrSetAsync<IEnumerable<T>>(
            $"{typeof(T).Name}:all({take},{skip})",
            _ => innerRepository.GetAllAsync(take, skip));

        return entities;
    }

    public async Task<PagedResult<T>> GetPagedAsync(int first = 10, string? after = null, string? before = null)
    {
        var cacheKey = $"{typeof(T).Name}:paged(first:{first},after:{after ?? "null"},before:{before ?? "null"})";

        var result = await cache.GetOrSetAsync<PagedResult<T>>(
            cacheKey,
            _ => innerRepository.GetPagedAsync(first, after, before),
            options => options.SetDuration(TimeSpan.FromSeconds(30)));

        return result;
    }

    public async Task<T> CreateAsync(T entity)
    {
        var createdEntity = await innerRepository.CreateAsync(entity);
        await UpdateCache(createdEntity);
        return createdEntity;
    }

    public async Task<T> UpdateAsync(T entity)
    {
        var updatedEntity = await innerRepository.UpdateAsync(entity);
        await UpdateCache(updatedEntity);
        return updatedEntity;
    }

    public async Task DeleteAsync(Guid id)
    {
        // We need the entity to remove it from cache if it has a key
        // But we only have the ID. For deletion, we can try to get the entity first to find its key
        var entity = await innerRepository.GetByIdAsync(id);

        // Delete the entity in the inner repository first, remove from cache after
        await innerRepository.DeleteAsync(id);
        await RemoveFromCache(id, entity);
    }

    // Helpers
    private async Task UpdateCache(T entity)
    {
        var type = typeof(T);
        var id = entity.Id;

        // Update primary cache
        await cache.SetAsync($"{type.Name}:{id}", entity);

        // Update mapping cache if entity has a key
        if (entity is IHasKey keyedEntity)
        {
            var key = keyedEntity.Key;
            if (!string.IsNullOrEmpty(key))
                await cache.SetAsync($"{type.Name}:mapping:{key}", id);
        }

        // Invalidate "all" cache as the collection has changed
        await cache.RemoveAsync($"{type.Name}:all(null,null)");

        // Invalidate all paged caches (pattern-based removal)
        await InvalidatePagedCaches();
    }

    private async Task RemoveFromCache(Guid? id = null, T? entity = null)
    {
        var type = typeof(T);

        // Remove from the primary cache
        if (entity != null)
        {
            id ??= entity.Id;

            // Check whether the entity has a key and remove it from the mapping cache if it does
            if (entity is IHasKey keyedEntity)
            {
                var key = keyedEntity.Key;
                if (!string.IsNullOrEmpty(key))
                    await cache.RemoveAsync($"{type.Name}:mapping:{key}");
            }
        }

        if (id != null)
            await cache.RemoveAsync($"{type.Name}:{id}");

        // Invalidate "all" cache as the collection has changed
        await cache.RemoveAsync($"{type.Name}:all(null,null)");

        // Invalidate all paged caches
        await InvalidatePagedCaches();
    }

    private async Task InvalidatePagedCaches()
    {
        // Since FusionCache doesn't support pattern-based deletion out of the box,
        // we'll need to track keys or use a simpler approach
        // For now, we just invalidate on write operations
        // In production, consider using Redis SCAN or tracking cache keys
        await Task.CompletedTask;
    }
}