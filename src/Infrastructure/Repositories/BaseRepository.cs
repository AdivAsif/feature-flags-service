using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public abstract class BaseRepository<TContext>(IDbContextFactory<TContext> contextFactory)
    where TContext : DbContext
{
    protected async Task<TResult> ExecuteAsync<TResult>(
        Func<TContext, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await operation(dbContext);
    }

    protected async Task ExecuteAsync(
        Func<TContext, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await contextFactory.CreateDbContextAsync(cancellationToken);
        await operation(dbContext);
    }
}