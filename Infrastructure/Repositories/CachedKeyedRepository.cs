using SharedKernel;
using ZiggyCreatures.Caching.Fusion;

namespace Infrastructure.Repositories;

public class CachedKeyedRepository<T>(IKeyedRepository<T> innerRepository, IFusionCache cache)
    : CachedRepository<T>(innerRepository, cache), IKeyedRepository<T> where T : EntityBase, IHasKey, ICacheable
{
    public async Task<T?> GetByKeyAsync(string key)
    {
        var typeName = typeof(T).Name;

        // Try to get the ID from the mapping cache (Key -> Id)
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
}
