using Service.Api.Data.Entities;

namespace Service.Api.Repositories;

public interface IComponentRepository : IEntityRepository<Component>
{
    /// <summary>Components with their boards, optionally filtered by a case-insensitive name/description
    /// search. All components when <paramref name="search"/> is null or blank.</summary>
    Task<IReadOnlyList<Component>> SearchAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary>Tracked component with its boards, for reads and updates. Null when no such component exists.</summary>
    Task<Component?> GetWithBoardsAsync(Guid id, CancellationToken cancellationToken = default);
}
