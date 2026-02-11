using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Repositories;

public class AuditLogsRepository(FeatureFlagsDbContext dbContext) : IRepository<AuditLog>
{
    // GET
    public async Task<AuditLog?> GetByIdAsync(Guid id)
    {
        return await dbContext.AuditLogs.FindAsync(id);
    }

    public async Task<IEnumerable<AuditLog>> GetAllAsync(int? take, int? skip)
    {
        return await dbContext.AuditLogs.AsNoTracking().ToListAsync();
    }

    public async Task<PagedResult<AuditLog>> GetPagedAsync(int first = 10, string? after = null,
        string? before = null)
    {
        first = Math.Clamp(first, 1, 100);

        var query = dbContext.AuditLogs.AsNoTracking().OrderBy(ff => ff.CreatedAt).ThenBy(ff => ff.Id);

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

        var totalCount = await dbContext.AuditLogs.CountAsync();

        var startCursor = items.Count > 0 ? CursorHelper.EncodeCursor(items.First().Id, items.First().CreatedAt) : null;
        var endCursor = items.Count > 0 ? CursorHelper.EncodeCursor(items.Last().Id, items.Last().CreatedAt) : null;

        var hasPreviousPage = !string.IsNullOrWhiteSpace(after) || (!string.IsNullOrWhiteSpace(before) && hasNextPage);

        return new PagedResult<AuditLog>
        {
            Items = items,
            PageInfo = new PageInfo
            {
                HasNextPage = string.IsNullOrWhiteSpace(before) && hasNextPage,
                HasPreviousPage = hasPreviousPage,
                StartCursor = startCursor,
                EndCursor = endCursor,
                TotalCount = totalCount
            }
        };
    }

    // CREATE
    public async Task<AuditLog> CreateAsync(AuditLog auditLog)
    {
        await dbContext.AuditLogs.AddAsync(auditLog);
        await dbContext.SaveChangesAsync();
        return auditLog;
    }

    // UPDATE
    public async Task<AuditLog> UpdateAsync(AuditLog auditLog)
    {
        dbContext.Entry(auditLog).State = EntityState.Modified;
        await dbContext.SaveChangesAsync();
        return auditLog;
    }

    // DELETE
    public async Task DeleteAsync(Guid id)
    {
        var auditLog = await dbContext.AuditLogs.FindAsync(id);
        if (auditLog == null) return;
        dbContext.AuditLogs.Remove(auditLog);
        await dbContext.SaveChangesAsync();
    }
}