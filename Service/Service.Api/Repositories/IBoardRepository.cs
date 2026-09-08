using Service.Api.Data.Entities;

namespace Service.Api.Repositories;

public interface IBoardRepository : IEntityRepository<Board>
{
    /// <summary>Boards with their components and orders, optionally filtered by a case-insensitive
    /// name/description search. All boards when <paramref name="search"/> is null or blank.</summary>
    Task<IReadOnlyList<Board>> SearchAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary>Tracked board with its components and orders, for reads and updates. Null when no such board
    /// exists.</summary>
    Task<Board?> GetWithRelationsAsync(Guid id, CancellationToken cancellationToken = default);
}
