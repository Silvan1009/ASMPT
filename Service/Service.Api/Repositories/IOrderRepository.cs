using Service.Api.Data.Entities;

namespace Service.Api.Repositories;

public interface IOrderRepository : IRepository<Order>
{
    /// <summary>Orders with their boards, optionally filtered by a case-insensitive name/description
    /// search. All orders when <paramref name="search"/> is null or blank.</summary>
    Task<IReadOnlyList<Order>> SearchAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary>Tracked order with its boards, for reads and updates. Null when no such order exists.</summary>
    Task<Order?> GetWithBoardsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Untracked order graph (boards and their components) for the download export. Null when no
    /// such order exists.</summary>
    Task<Order?> GetForExportAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes the orders with the given ids in one statement; join rows cascade in the database.
    /// Returns the number of rows deleted.</summary>
    Task<int> DeleteByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
