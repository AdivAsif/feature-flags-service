namespace SharedKernel;

public interface IReadRepository<T> where T : EntityBase
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync(int? take = null, int? skip = null);
    Task<PagedResult<T>> GetPagedAsync(int first = 10, string? after = null, string? before = null);
}