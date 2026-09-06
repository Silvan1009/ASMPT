using Service.Api.Data.Entities;
using Service.Api.ErrorHandling;
using Service.Api.Models;
using Service.Api.Repositories;

namespace Service.Api.Services;

public sealed class BoardService(IBoardRepository boards, IComponentRepository components) : IBoardService
{
    public async Task<IReadOnlyList<BoardDto>> SearchAsync(string? search, CancellationToken cancellationToken = default)
        => (await boards.SearchAsync(search, cancellationToken)).Select(ToDto).ToList();

    public async Task<BoardDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => await boards.GetWithComponentsAsync(id, cancellationToken) is { } board ? ToDto(board) : null;

    public async Task<BoardDto> CreateAsync(BoardRequest request, CancellationToken cancellationToken = default)
    {
        var board = new Board
        {
            Name = request.Name.Trim(),
            Description = Normalize(request.Description),
            Length = request.Length,
            Width = request.Width,
        };
        board.Components.AddRange(await ResolveComponentsAsync(request.ComponentIds, cancellationToken));

        await boards.AddAsync(board, cancellationToken);
        await boards.SaveChangesAsync(cancellationToken);
        return ToDto(board);
    }

    public async Task<BoardDto?> UpdateAsync(Guid id, BoardRequest request, CancellationToken cancellationToken = default)
    {
        var board = await boards.GetWithComponentsAsync(id, cancellationToken);
        if (board is null)
        {
            return null;
        }

        board.Name = request.Name.Trim();
        board.Description = Normalize(request.Description);
        board.Length = request.Length;
        board.Width = request.Width;

        // Diff the skip navigation: EF Core turns removed/added Component instances into deleted/inserted
        // BoardComponents rows on SaveChanges.
        var wanted = await ResolveComponentsAsync(request.ComponentIds, cancellationToken);
        board.Components.RemoveAll(existing => wanted.All(w => w.Id != existing.Id));
        board.Components.AddRange(wanted.Where(w => board.Components.All(existing => existing.Id != w.Id)));

        await boards.SaveChangesAsync(cancellationToken);
        return ToDto(board);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => await boards.DeleteByIdsAsync([id], cancellationToken) == 1;

    public Task DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => boards.DeleteByIdsAsync(ids, cancellationToken);

    private async Task<IReadOnlyList<Component>> ResolveComponentsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToArray();
        var found = await components.GetByIdsAsync(distinct, cancellationToken);
        if (found.Count == distinct.Length)
        {
            return found;
        }

        var missing = distinct.Except(found.Select(c => c.Id)).ToArray();
        throw new RelatedEntitiesNotFoundException(nameof(BoardRequest.ComponentIds), "component", missing);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static BoardDto ToDto(Board board) => new(
        board.Id,
        board.Name,
        board.Description,
        board.Length,
        board.Width,
        board.Components.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Select(c => new EntityRefDto(c.Id, c.Name)).ToList(),
        board.Orders.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase).Select(o => new EntityRefDto(o.Id, o.Name)).ToList());
}
