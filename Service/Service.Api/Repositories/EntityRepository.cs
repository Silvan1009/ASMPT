using Microsoft.EntityFrameworkCore;
using Service.Api.Data;
using Service.Api.Data.Entities;

namespace Service.Api.Repositories;

/// <summary>
/// Base class of the entity-specific repositories: adds the id-based bulk operations of
/// <see cref="IEntityRepository{TEntity}"/> to the generic <see cref="Repository{TEntity}"/>.
/// </summary>
public class EntityRepository<TEntity>(ApplicationDbContext dbContext) : Repository<TEntity>(dbContext), IEntityRepository<TEntity>
    where TEntity : class, IEntity
{
    public async Task<IReadOnlyList<TEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => await Set.Where(e => ids.Contains(e.Id)).ToListAsync(cancellationToken);

    public Task<int> DeleteByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => Set.Where(e => ids.Contains(e.Id)).ExecuteDeleteAsync(cancellationToken);
}
