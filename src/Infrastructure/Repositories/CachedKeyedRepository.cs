using SharedKernel;
using ZiggyCreatures.Caching.Fusion;

namespace Infrastructure.Repositories;

public class CachedKeyedRepository<T>(IKeyedRepository<T> innerRepository, IFusionCache cache)
    : CachedRepository<T>(innerRepository, cache), IKeyedRepository<T> where T : EntityBase, IHasKey, ICacheable
{
    /// <summary>
    ///     Get by key WITHOUT projectId - only for non-multi-tenant or legacy code.
    ///     For multi-tenant entities, this is unsafe and should be avoided.
    /// </summary>
    public async Task<T?> GetByKeyAsync(string key)
    {
        var typeName = typeof(T).Name;

        // For multi-tenant entities, warn but still work (fetch first match)
        if (typeof(IMultiTenant).IsAssignableFrom(typeof(T)))
        {
            // Not cached for multi-tenant without projectId (ambiguous)
            var entity = await innerRepository.GetByKeyAsync(key);
            return entity;
        }

        // Single-tenant: Use caching with key->id mapping
        var id = await cache.GetOrSetAsync(
            $"{typeName}:mapping:{key}",
            async _ =>
            {
                var entity = await innerRepository.GetByKeyAsync(key);
                return entity?.Id;
            });

        if (id == null) return null;

        return await GetByIdAsync(id.Value);
    }

    /// <summary>
    ///     Get by key WITH projectId - proper multi-tenant version with caching.
    ///     Cache key format: {TypeName}:{ProjectId}:key:{Key} -> Id
    /// </summary>
    public async Task<T?> GetByKeyAsync(Guid projectId, string key)
    {
        var typeName = typeof(T).Name;

        // Cache the key->id mapping with projectId scope
        var mappingCacheKey = $"{typeName}:{projectId}:key:{key}";

        var id = await cache.GetOrSetAsync(
            mappingCacheKey,
            async _ =>
            {
                var entity = await innerRepository.GetByKeyAsync(projectId, key);
                return entity?.Id;
            },
            options => options.SetDuration(TimeSpan.FromMinutes(5)));

        if (id == null) return null;

        // Now get the full entity (also cached with project scope)
        var entityCacheKey = $"{typeName}:{projectId}:{id.Value}";
        var cachedEntity = await cache.GetOrSetAsync(
            entityCacheKey,
            async _ => await innerRepository.GetByKeyAsync(projectId, key),
            options => options.SetDuration(TimeSpan.FromMinutes(5)));

        return cachedEntity;
    }
}