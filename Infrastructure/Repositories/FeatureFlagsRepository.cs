using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Repositories;

public sealed class FeatureFlagsRepository(FeatureFlagsDbContext dbContext) : IRepository<FeatureFlag>
{
    // GET
    public async Task<FeatureFlag?> GetByIdAsync(Guid id)
    {
        return await dbContext.FeatureFlags.FindAsync(id);
    }

    public async Task<FeatureFlag?> GetByKeyAsync(string key)
    {
        return await dbContext.FeatureFlags.AsNoTracking().FirstOrDefaultAsync(ff => ff.Key == key);
    }

    public async Task<IEnumerable<FeatureFlag>> GetAllAsync(int? take, int? skip)
    {
        return await dbContext.FeatureFlags.AsNoTracking().ToListAsync();
    }

    public async Task<PagedResult<FeatureFlag>> GetPagedAsync(int first = 10, string? after = null,
        string? before = null)
    {
        first = Math.Clamp(first, 1, 100);

        var query = dbContext.FeatureFlags.AsNoTracking().OrderBy(ff => ff.CreatedAt).ThenBy(ff => ff.Id);

        if (!string.IsNullOrWhiteSpace(after) &&
            CursorHelper.TryDecodeCursor(after, out var afterId, out var afterCreatedAt))
            query = query
                .Where(ff => ff.CreatedAt > afterCreatedAt || (ff.CreatedAt == afterCreatedAt && ff.Id > afterId))
                .OrderBy(ff => ff.CreatedAt)
                .ThenBy(ff => ff.Id);
        else if (!string.IsNullOrWhiteSpace(before) &&
                 CursorHelper.TryDecodeCursor(before, out var beforeId, out var beforeCreatedAt))
            query = query
                .Where(ff => ff.CreatedAt < beforeCreatedAt || (ff.CreatedAt == beforeCreatedAt && ff.Id < beforeId))
                .OrderByDescending(ff => ff.CreatedAt)
                .ThenByDescending(ff => ff.Id);

        var items = await query.Take(first + 1).ToListAsync();

        var hasNextPage = items.Count > first;
        if (hasNextPage) items = items.Take(first).ToList();

        if (!string.IsNullOrWhiteSpace(before)) items.Reverse();

        var totalCount = await dbContext.FeatureFlags.CountAsync();

        var startCursor = items.Count > 0 ? CursorHelper.EncodeCursor(items.First().Id, items.First().CreatedAt) : null;
        var endCursor = items.Count > 0 ? CursorHelper.EncodeCursor(items.Last().Id, items.Last().CreatedAt) : null;

        var hasPreviousPage = !string.IsNullOrWhiteSpace(after) || (!string.IsNullOrWhiteSpace(before) && hasNextPage);

        return new PagedResult<FeatureFlag>
        {
            Items = items,
            PageInfo = new PageInfo
            {
                HasNextPage = !string.IsNullOrWhiteSpace(before) ? false : hasNextPage,
                HasPreviousPage = hasPreviousPage,
                StartCursor = startCursor,
                EndCursor = endCursor,
                TotalCount = totalCount
            }
        };
    }

    // CREATE
    public async Task<FeatureFlag> CreateAsync(FeatureFlag featureFlag)
    {
        await dbContext.FeatureFlags.AddAsync(featureFlag);
        await dbContext.SaveChangesAsync();
        return featureFlag;
    }

    // UPDATE
    public async Task<FeatureFlag> UpdateAsync(FeatureFlag featureFlag)
    {
        dbContext.Entry(featureFlag).State = EntityState.Modified;
        // dbContext.FeatureFlags.Update(featureFlag);
        await dbContext.SaveChangesAsync();
        return featureFlag;
    }

    // DELETE
    public async Task DeleteAsync(Guid id)
    {
        var featureFlag = await dbContext.FeatureFlags.FindAsync(id);
        if (featureFlag == null) return;
        dbContext.FeatureFlags.Remove(featureFlag);
        await dbContext.SaveChangesAsync();
    }
}