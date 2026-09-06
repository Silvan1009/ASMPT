using Service.Api.Data.Entities;

namespace Service.Api.Repositories;

public interface IBoardRepository : IRepository<Board>
{
    /// <summary>Boards with their components and orders, optionally filtered by a case-insensitive
    /// name/description search. All boards when <paramref name="search"/> is null or blank.</summary>
    Task<IReadOnlyList<Board>> SearchAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary>Tracked board with its components, for reads and updates. Null when no such board exists.</summary>
    Task<Board?> GetWithComponentsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked boards for the given ids, used to resolve an order's requested board ids.</summary>
    Task<IReadOnlyList<Board>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Deletes the boards with the given ids in one statement; join rows cascade in the database.
    /// Returns the number of rows deleted.</summary>
    Task<int> DeleteByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
