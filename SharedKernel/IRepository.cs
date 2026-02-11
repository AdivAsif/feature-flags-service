namespace SharedKernel;

public interface IRepository<T> where T : EntityBase
{
    public Task<T?> GetByIdAsync(Guid id);
    public Task<T?> GetByKeyAsync(string key);
    public Task<IEnumerable<T>> GetAllAsync(int? take = null, int? skip = null);
    public Task<PagedResult<T>> GetPagedAsync(int first = 10, string? after = null, string? before = null);
    public Task<T> CreateAsync(T entity);
    public Task<T> UpdateAsync(T entity);
    public Task DeleteAsync(Guid id);
}