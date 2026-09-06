using System.Linq.Expressions;

namespace Service.Api.Repositories;

/// <summary>
/// Generic data-access abstraction over an EF Core <c>DbSet</c>. Depend on this (not
/// <c>ApplicationDbContext</c> directly) from business logic so it can be mocked in unit tests
/// without touching a real or in-memory database.
/// </summary>
public interface IRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default);

    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    void Update(TEntity entity);

    void Remove(TEntity entity);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
