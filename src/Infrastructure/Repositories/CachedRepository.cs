using SharedKernel;
using ZiggyCreatures.Caching.Fusion;

namespace Infrastructure.Repositories;

public class CachedRepository<T>(IRepository<T> innerRepository, IFusionCache cache)
    : IRepository<T> where T : EntityBase, ICacheable
{
    public async Task<T?> GetByIdAsync(Guid id)
    {
        var cacheKey = GetCacheKey(id);

        var entity = await cache.GetOrSetAsync<T>(
            cacheKey,
            _ => innerRepository.GetByIdAsync(id));

        return entity;
    }

    public async Task<IEnumerable<T>> GetAllAsync(int? take, int? skip)
    {
        // GetAllAsync should NEVER be cached across projects
        // This would be a security issue - always query DB
        var entities = await innerRepository.GetAllAsync(take, skip);
        return entities;
    }

    public async Task<PagedResult<T>> GetPagedAsync(int first = 10, string? after = null, string? before = null)
    {
        // GetPagedAsync should NEVER be cached across projects
        // This would be a security issue - always query DB
        var result = await innerRepository.GetPagedAsync(first, after, before);
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

    /// <summary>
    ///     Generate cache key with project scoping if entity is multi-tenant.
    ///     Format: {TypeName}:{ProjectId}:{Id} for multi-tenant
    ///     Format: {TypeName}:{Id} for single-tenant
    /// </summary>
    private static string GetCacheKey(Guid id, Guid? projectId = null)
    {
        var typeName = typeof(T).Name;

        // If T implements IMultiTenant, we need project-scoped keys
        if (typeof(IMultiTenant).IsAssignableFrom(typeof(T)))
        {
            // If projectId is provided, use it
            if (projectId.HasValue)
                return $"{typeName}:{projectId.Value}:{id}";

            // If not provided, use a placeholder (will be set on read from DB)
            return $"{typeName}:*:{id}";
        }

        // Non-multi-tenant entities use simple key
        return $"{typeName}:{id}";
    }

    /// <summary>
    ///     Generate cache key for key-based lookup (multi-tenant aware).
    ///     Format: {TypeName}:{ProjectId}:key:{Key}
    /// </summary>
    private static string GetKeyMappingCacheKey(string key, Guid? projectId = null)
    {
        var typeName = typeof(T).Name;

        if (typeof(IMultiTenant).IsAssignableFrom(typeof(T)))
        {
            if (projectId.HasValue)
                return $"{typeName}:{projectId.Value}:key:{key}";

            return $"{typeName}:*:key:{key}";
        }

        return $"{typeName}:mapping:{key}";
    }

    /// <summary>
    ///     Extract ProjectId from entity if it implements IMultiTenant.
    /// </summary>
    private static Guid? GetProjectId(T? entity)
    {
        return entity is IMultiTenant multiTenant ? multiTenant.ProjectId : null;
    }

    // Helpers
    private async Task UpdateCache(T entity)
    {
        var projectId = GetProjectId(entity);
        var id = entity.Id;

        // Update primary cache with project-scoped key
        var cacheKey = GetCacheKey(id, projectId);
        await cache.SetAsync(cacheKey, entity);

        // Update mapping cache if entity has a key
        if (entity is IHasKey keyedEntity)
        {
            var key = keyedEntity.Key;
            if (!string.IsNullOrEmpty(key))
            {
                var mappingKey = GetKeyMappingCacheKey(key, projectId);
                await cache.SetAsync(mappingKey, id);
            }
        }
    }

    private async Task RemoveFromCache(Guid? id = null, T? entity = null)
    {
        var projectId = GetProjectId(entity);

        // Remove from the primary cache
        if (entity != null)
        {
            id ??= entity.Id;

            // Check whether the entity has a key and remove it from the mapping cache if it does
            if (entity is IHasKey keyedEntity)
            {
                var key = keyedEntity.Key;
                if (!string.IsNullOrEmpty(key))
                {
                    var mappingKey = GetKeyMappingCacheKey(key, projectId);
                    await cache.RemoveAsync(mappingKey);
                }
            }
        }

        if (id != null)
        {
            var cacheKey = GetCacheKey(id.Value, projectId);
            await cache.RemoveAsync(cacheKey);
        }
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