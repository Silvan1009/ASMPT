using Microsoft.EntityFrameworkCore;
using Service.Api.Data;

namespace Service.Api.Repositories;

/// <summary>
/// Default <see cref="IRepository{TEntity}"/> implementation backed by
/// <see cref="ApplicationDbContext"/>. Registered as an open generic in DI, so
/// <c>IRepository&lt;SomeEntity&gt;</c> resolves without any per-entity wiring.
/// </summary>
public class Repository<TEntity>(ApplicationDbContext dbContext) : IRepository<TEntity>
    where TEntity : class
{
    private readonly DbSet<TEntity> _set = dbContext.Set<TEntity>();

    public async Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        => await _set.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _set.ToListAsync(cancellationToken);

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => await _set.AddAsync(entity, cancellationToken);

    public void Update(TEntity entity) => _set.Update(entity);

    public void Remove(TEntity entity) => _set.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
