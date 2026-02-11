namespace SharedKernel;

public interface IRepository<T> : IReadRepository<T> where T : EntityBase
{
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task DeleteAsync(Guid id);
}

public interface IKeyedRepository<T> : IRepository<T> where T : EntityBase, IHasKey
{
    Task<T?> GetByKeyAsync(string key);
    Task<T?> GetByKeyAsync(Guid projectId, string key); // Multi-tenant version
}