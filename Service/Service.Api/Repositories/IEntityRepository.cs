using Service.Api.Data.Entities;

namespace Service.Api.Repositories;

/// <summary>
/// <see cref="IRepository{TEntity}"/> plus the id-based bulk operations shared by every entity keyed by a
/// <see cref="Guid"/> (orders, boards, components).
/// </summary>
public interface IEntityRepository<TEntity> : IRepository<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>Tracked entities for the given ids. Ids that do not exist are simply absent from the result.</summary>
    Task<IReadOnlyList<TEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Deletes the entities with the given ids in one statement; join rows cascade in the database.
    /// Returns the number of rows deleted.</summary>
    Task<int> DeleteByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
