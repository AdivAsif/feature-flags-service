namespace SharedKernel;

public interface IReadRepository<T> where T : EntityBase
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<T>> GetPagedAsync(int first = 10, string? after = null, string? before = null,
        CancellationToken cancellationToken = default);
}