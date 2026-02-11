using Application.Common;
using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class FeatureFlagsRepository(IDbContextFactory<FeatureFlagsDbContext> contextFactory)
    : BaseRepository<FeatureFlagsDbContext>(contextFactory), IFeatureFlagRepository
{
    private static readonly Func<FeatureFlagsDbContext, Guid, string, CancellationToken, Task<FeatureFlag?>>
        GetByKeyCompiledQuery =
            EF.CompileAsyncQuery((FeatureFlagsDbContext db, Guid projectId, string key, CancellationToken ct) =>
                db.FeatureFlags
                    .AsNoTracking()
                    .FirstOrDefault(ff => ff.ProjectId == projectId && ff.Key == key)
            );

    // GET
    public Task<FeatureFlag?> GetByIdAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(db => db.FeatureFlags
                .AsNoTracking()
                .FirstOrDefaultAsync(ff => ff.ProjectId == projectId && ff.Id == id, cancellationToken),
            cancellationToken);
    }

    public Task<FeatureFlag?> GetByKeyAsync(Guid projectId, string key, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(db => GetByKeyCompiledQuery(db, projectId, key, cancellationToken), cancellationToken);
    }

    public Task<Slice<FeatureFlag>> GetPagedAsync(
        Guid projectId,
        int first = 10,
        string? after = null,
        string? before = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async db =>
        {
            first = Math.Clamp(first, 1, 100);

            var query = db.FeatureFlags.AsNoTracking();

            if (projectId != Guid.Empty)
                query = query.Where(ff => ff.ProjectId == projectId);

            query = query.OrderBy(ff => ff.CreatedAt).ThenBy(ff => ff.Id);

            if (!string.IsNullOrWhiteSpace(after) &&
                CursorHelper.TryDecodeCursor(after, out var afterId, out var afterCreatedAt))
                query = query
                    .Where(ff => ff.CreatedAt > afterCreatedAt || (ff.CreatedAt == afterCreatedAt && ff.Id > afterId))
                    .OrderBy(ff => ff.CreatedAt)
                    .ThenBy(ff => ff.Id);
            else if (!string.IsNullOrWhiteSpace(before) &&
                     CursorHelper.TryDecodeCursor(before, out var beforeId, out var beforeCreatedAt))
                query = query
                    .Where(ff =>
                        ff.CreatedAt < beforeCreatedAt || (ff.CreatedAt == beforeCreatedAt && ff.Id < beforeId))
                    .OrderByDescending(ff => ff.CreatedAt)
                    .ThenByDescending(ff => ff.Id);

            var items = await query.Take(first + 1).ToListAsync(cancellationToken);

            var hasNextPage = items.Count > first;
            if (hasNextPage) items = items.Take(first).ToList();

            if (!string.IsNullOrWhiteSpace(before)) items.Reverse();

            var totalCount = projectId != Guid.Empty
                ? await db.FeatureFlags.CountAsync(ff => ff.ProjectId == projectId, cancellationToken)
                : await db.FeatureFlags.CountAsync(cancellationToken);

            var startCursor = items.Count > 0
                ? CursorHelper.EncodeCursor(items.First().Id, items.First().CreatedAt)
                : null;
            var endCursor = items.Count > 0 ? CursorHelper.EncodeCursor(items.Last().Id, items.Last().CreatedAt) : null;

            var hasPreviousPage =
                !string.IsNullOrWhiteSpace(after) || (!string.IsNullOrWhiteSpace(before) && hasNextPage);

            return new Slice<FeatureFlag>
            {
                Items = items,
                HasNextPage = string.IsNullOrWhiteSpace(before) && hasNextPage,
                HasPreviousPage = hasPreviousPage,
                StartCursor = startCursor,
                EndCursor = endCursor,
                TotalCount = totalCount
            };
        }, cancellationToken);
    }

    // CREATE
    public Task<FeatureFlag> CreateAsync(FeatureFlag featureFlag, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async db =>
        {
            await db.FeatureFlags.AddAsync(featureFlag, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return featureFlag;
        }, cancellationToken);
    }

    // UPDATE
    public Task<FeatureFlag> UpdateAsync(FeatureFlag featureFlag, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async db =>
        {
            db.Entry(featureFlag).State = EntityState.Modified;
            await db.SaveChangesAsync(cancellationToken);
            return featureFlag;
        }, cancellationToken);
    }

    // DELETE
    public Task DeleteAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async db =>
        {
            if (db.Database.IsRelational())
            {
                await db.FeatureFlags
                    .Where(ff => ff.ProjectId == projectId && ff.Id == id)
                    .ExecuteDeleteAsync(cancellationToken);
            }
            else
            {
                var featureFlag = await db.FeatureFlags
                    .FirstOrDefaultAsync(ff => ff.ProjectId == projectId && ff.Id == id, cancellationToken);
                if (featureFlag != null)
                {
                    db.FeatureFlags.Remove(featureFlag);
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
        }, cancellationToken);
    }
}