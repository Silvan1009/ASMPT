using Service.Api.Data.Entities;

namespace Service.Api.Repositories;

public interface IComponentRepository : IRepository<Component>
{
    /// <summary>Components with their boards, optionally filtered by a case-insensitive name/description
    /// search. All components when <paramref name="search"/> is null or blank.</summary>
    Task<IReadOnlyList<Component>> SearchAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary>Tracked components for the given ids, used to resolve a board's requested component ids.</summary>
    Task<IReadOnlyList<Component>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Tracked component with its boards, for reads and updates. Null when no such component exists.</summary>
    Task<Component?> GetWithBoardsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes the components with the given ids in one statement; join rows cascade in the
    /// database. Returns the number of rows deleted.</summary>
    Task<int> DeleteByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
