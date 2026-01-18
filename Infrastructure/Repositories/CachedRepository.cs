using System.ComponentModel.DataAnnotations;
using SharedKernel;
using ZiggyCreatures.Caching.Fusion;

namespace Infrastructure.Repositories;

public class CachedRepository<T>(IRepository<T> innerRepository, IFusionCache cache)
    : IRepository<T> where T : EntityBase
{
    public async Task<T?> GetByIdAsync(Guid id)
    {
        var entity = await cache.GetOrSetAsync<T>(
            $"{typeof(T).Name}:{id}",
            _ => innerRepository.GetByIdAsync(id));

        return entity;
    }

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

    public async Task<IEnumerable<T>> GetAllAsync(int? take, int? skip)
    {
        var entities = await cache.GetOrSetAsync<IEnumerable<T>>(
            $"{typeof(T).Name}:all({take},{skip})",
            _ => innerRepository.GetAllAsync(take, skip));

        return entities;
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
        await RemoveFromCache(id: id, entity: entity);
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
    }
}