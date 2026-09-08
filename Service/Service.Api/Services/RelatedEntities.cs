using Service.Api.Data.Entities;
using Service.Api.ErrorHandling;
using Service.Api.Repositories;

namespace Service.Api.Services;

/// <summary>Shared handling of the related-entity id lists in requests (an order's boards, a board's components).</summary>
internal static class RelatedEntities
{
    /// <summary>
    /// Loads the entities for <paramref name="ids"/> (duplicates are ignored). Throws a
    /// <see cref="RelatedEntitiesNotFoundException"/> naming <paramref name="propertyName"/> when any id does not
    /// exist, which the API reports as a 400 validation problem on that property.
    /// </summary>
    public static async Task<IReadOnlyList<TEntity>> ResolveAsync<TEntity>(
        IReadOnlyList<Guid> ids,
        IEntityRepository<TEntity> repository,
        string propertyName,
        string entityName,
        CancellationToken cancellationToken)
        where TEntity : class, IEntity
    {
        var distinct = ids.Distinct().ToArray();
        var found = await repository.GetByIdsAsync(distinct, cancellationToken);
        if (found.Count == distinct.Length)
        {
            return found;
        }

        var missing = distinct.Except(found.Select(e => e.Id)).ToArray();
        throw new RelatedEntitiesNotFoundException(propertyName, entityName, missing);
    }

    /// <summary>
    /// Makes <paramref name="target"/> (a tracked skip navigation) contain exactly <paramref name="wanted"/>,
    /// keeping the instances that are already present so EF Core only deletes and inserts the join rows that
    /// actually changed.
    /// </summary>
    public static void SyncTo<TEntity>(this List<TEntity> target, IReadOnlyList<TEntity> wanted)
        where TEntity : class, IEntity
    {
        var wantedIds = wanted.Select(e => e.Id).ToHashSet();
        target.RemoveAll(existing => !wantedIds.Contains(existing.Id));

        var existingIds = target.Select(e => e.Id).ToHashSet();
        target.AddRange(wanted.Where(e => !existingIds.Contains(e.Id)));
    }
}
