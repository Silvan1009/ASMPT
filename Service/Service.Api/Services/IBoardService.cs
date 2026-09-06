using Service.Api.Models;

namespace Service.Api.Services;

public interface IBoardService
{
    Task<IReadOnlyList<BoardDto>> SearchAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary>Null when no such board exists.</summary>
    Task<BoardDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BoardDto> CreateAsync(BoardRequest request, CancellationToken cancellationToken = default);

    /// <summary>Null when no such board exists.</summary>
    Task<BoardDto?> UpdateAsync(Guid id, BoardRequest request, CancellationToken cancellationToken = default);

    /// <summary>False when no such board exists.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes several boards at once; unknown ids are ignored.</summary>
    Task DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
