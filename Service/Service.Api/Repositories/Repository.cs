using System.Linq.Expressions;
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
    /// <summary>For derived, entity-specific repositories that need more than the generic surface above
    /// (e.g. <c>Include</c>, <c>ExecuteDeleteAsync</c>).</summary>
    protected ApplicationDbContext DbContext { get; } = dbContext;

    protected DbSet<TEntity> Set { get; } = dbContext.Set<TEntity>();

    public async Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        => await Set.FindAsync([id], cancellationToken);

    public async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => await Set.FirstOrDefaultAsync(predicate, cancellationToken);

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Set.ToListAsync(cancellationToken);

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        => await Set.AddAsync(entity, cancellationToken);

    public void Update(TEntity entity) => Set.Update(entity);

    public void Remove(TEntity entity) => Set.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => DbContext.SaveChangesAsync(cancellationToken);
}
