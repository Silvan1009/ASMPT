using Service.Api.Data.Entities;
using Service.Api.Models;
using Service.Api.Repositories;

namespace Service.Api.Services;

public sealed class BoardService(IBoardRepository boards, IComponentRepository components, ILogger<BoardService> logger) : IBoardService
{
    public async Task<IReadOnlyList<BoardDto>> SearchAsync(string? search, CancellationToken cancellationToken = default)
        => (await boards.SearchAsync(search, cancellationToken)).Select(ToDto).ToList();

    public async Task<BoardDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => await boards.GetWithRelationsAsync(id, cancellationToken) is { } board ? ToDto(board) : null;

    public async Task<BoardDto> CreateAsync(BoardRequest request, CancellationToken cancellationToken = default)
    {
        var board = new Board
        {
            Name = RequestText.Required(request.Name),
            Description = RequestText.Optional(request.Description),
            Length = request.Length,
            Width = request.Width,
        };
        board.Components.AddRange(await ResolveComponentsAsync(request.ComponentIds, cancellationToken));

        await boards.AddAsync(board, cancellationToken);
        await boards.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Board {BoardId} created with {ComponentCount} component(s)", board.Id, board.Components.Count);
        return ToDto(board);
    }

    public async Task<BoardDto?> UpdateAsync(Guid id, BoardRequest request, CancellationToken cancellationToken = default)
    {
        var board = await boards.GetWithRelationsAsync(id, cancellationToken);
        if (board is null)
        {
            return null;
        }

        board.Name = RequestText.Required(request.Name);
        board.Description = RequestText.Optional(request.Description);
        board.Length = request.Length;
        board.Width = request.Width;

        // EF Core turns the components added to / removed from the skip navigation into inserted / deleted
        // BoardComponents rows on SaveChanges.
        board.Components.SyncTo(await ResolveComponentsAsync(request.ComponentIds, cancellationToken));

        await boards.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Board {BoardId} updated", board.Id);
        return ToDto(board);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await boards.DeleteByIdsAsync([id], cancellationToken) == 1;
        if (deleted)
        {
            logger.LogInformation("Board {BoardId} deleted", id);
        }

        return deleted;
    }

    public async Task DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var deleted = await boards.DeleteByIdsAsync(ids, cancellationToken);
        logger.LogInformation("Deleted {Deleted} of {Requested} board(s)", deleted, ids.Count);
    }

    private Task<IReadOnlyList<Component>> ResolveComponentsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
        => RelatedEntities.ResolveAsync(ids, components, nameof(BoardRequest.ComponentIds), "component", cancellationToken);

    private static BoardDto ToDto(Board board) => new(
        board.Id,
        board.Name,
        board.Description,
        board.Length,
        board.Width,
        board.Components.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Select(c => new EntityRefDto(c.Id, c.Name)).ToList(),
        board.Orders.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase).Select(o => new EntityRefDto(o.Id, o.Name)).ToList());
}
