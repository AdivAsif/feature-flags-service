using Application.Common;
using Application.Interfaces.Repositories;
using Domain;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class AuditLogsRepository(IDbContextFactory<FeatureFlagsDbContext> contextFactory)
    : BaseRepository<FeatureFlagsDbContext>(contextFactory), IAuditLogRepository
{
    // GET
    public Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async db => await db.AuditLogs.FindAsync([id], cancellationToken),
            cancellationToken);
    }

    public Task<Slice<AuditLog>> GetPagedAsync(int first = 10, string? after = null,
        string? before = null, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async db =>
        {
            first = Math.Clamp(first, 1, 100);

            var query = db.AuditLogs.AsNoTracking().OrderBy(al => al.CreatedAt).ThenBy(al => al.Id);

            if (!string.IsNullOrWhiteSpace(after) &&
                CursorHelper.TryDecodeCursor(after, out var afterId, out var afterCreatedAt))
                query = query
                    .Where(al => al.CreatedAt > afterCreatedAt || (al.CreatedAt == afterCreatedAt && al.Id > afterId))
                    .OrderBy(al => al.CreatedAt)
                    .ThenBy(al => al.Id);
            else if (!string.IsNullOrWhiteSpace(before) &&
                     CursorHelper.TryDecodeCursor(before, out var beforeId, out var beforeCreatedAt))
                query = query
                    .Where(al =>
                        al.CreatedAt < beforeCreatedAt || (al.CreatedAt == beforeCreatedAt && al.Id < beforeId))
                    .OrderByDescending(al => al.CreatedAt)
                    .ThenByDescending(al => al.Id);

            var items = await query.Take(first + 1).ToListAsync(cancellationToken);

            var hasNextPage = items.Count > first;
            if (hasNextPage) items = items.Take(first).ToList();

            if (!string.IsNullOrWhiteSpace(before)) items.Reverse();

            var totalCount = await db.AuditLogs.CountAsync(cancellationToken);

            var startCursor = items.Count > 0
                ? CursorHelper.EncodeCursor(items.First().Id, items.First().CreatedAt)
                : null;
            var endCursor = items.Count > 0 ? CursorHelper.EncodeCursor(items.Last().Id, items.Last().CreatedAt) : null;

            var hasPreviousPage =
                !string.IsNullOrWhiteSpace(after) || (!string.IsNullOrWhiteSpace(before) && hasNextPage);

            return new Slice<AuditLog>
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
    public Task<AuditLog> CreateAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async db =>
        {
            await db.AuditLogs.AddAsync(auditLog, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return auditLog;
        }, cancellationToken);
    }

    // DELETE
    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async db => await db.AuditLogs
            .Where(al => al.Id == id)
            .ExecuteDeleteAsync(cancellationToken), cancellationToken);
    }

    // UPDATE - not needed
    public Task<AuditLog> UpdateAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async db =>
        {
            db.Entry(auditLog).State = EntityState.Modified;
            await db.SaveChangesAsync(cancellationToken);
            return auditLog;
        }, cancellationToken);
    }
}