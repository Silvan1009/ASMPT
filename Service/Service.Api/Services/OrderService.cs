using System.Text.Json;
using Service.Api.Data.Entities;
using Service.Api.ErrorHandling;
using Service.Api.Models;
using Service.Api.Repositories;

namespace Service.Api.Services;

public sealed class OrderService(IOrderRepository orders, IBoardRepository boards, TimeProvider clock) : IOrderService
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<IReadOnlyList<OrderDto>> SearchAsync(string? search, CancellationToken cancellationToken = default)
        => (await orders.SearchAsync(search, cancellationToken)).Select(ToDto).ToList();

    public async Task<OrderDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => await orders.GetWithBoardsAsync(id, cancellationToken) is { } order ? ToDto(order) : null;

    public async Task<OrderDto> CreateAsync(OrderRequest request, CancellationToken cancellationToken = default)
    {
        var order = new Order
        {
            Name = request.Name.Trim(),
            Description = Normalize(request.Description),
            OrderDate = request.OrderDate,
        };
        order.Boards.AddRange(await ResolveBoardsAsync(request.BoardIds, cancellationToken));

        await orders.AddAsync(order, cancellationToken);
        await orders.SaveChangesAsync(cancellationToken);
        return ToDto(order);
    }

    public async Task<OrderDto?> UpdateAsync(Guid id, OrderRequest request, CancellationToken cancellationToken = default)
    {
        var order = await orders.GetWithBoardsAsync(id, cancellationToken);
        if (order is null)
        {
            return null;
        }

        order.Name = request.Name.Trim();
        order.Description = Normalize(request.Description);
        order.OrderDate = request.OrderDate;

        // Diff the skip navigation: EF Core turns removed/added Board instances into deleted/inserted
        // OrderBoards rows on SaveChanges.
        var wanted = await ResolveBoardsAsync(request.BoardIds, cancellationToken);
        order.Boards.RemoveAll(existing => wanted.All(w => w.Id != existing.Id));
        order.Boards.AddRange(wanted.Where(w => order.Boards.All(existing => existing.Id != w.Id)));

        await orders.SaveChangesAsync(cancellationToken);
        return ToDto(order);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => await orders.DeleteByIdsAsync([id], cancellationToken) == 1;

    public Task DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => orders.DeleteByIdsAsync(ids, cancellationToken);

    public async Task<OrderExportFile?> ExportAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await orders.GetForExportAsync(id, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var export = new OrderExportDto(
            order.Id,
            order.Name,
            order.Description,
            order.OrderDate,
            clock.GetUtcNow(),
            order.Boards.Select(b => new BoardExportDto(
                b.Id,
                b.Name,
                b.Description,
                b.Length,
                b.Width,
                b.Components.Select(c => new ComponentExportDto(c.Id, c.Name, c.Description, c.Quantity)).ToList())).ToList());

        var fileName = $"order-{FileNames.Sanitize(order.Name, order.Id.ToString())}.json";
        return new OrderExportFile(fileName, JsonSerializer.SerializeToUtf8Bytes(export, ExportJsonOptions));
    }

    private async Task<IReadOnlyList<Board>> ResolveBoardsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToArray();
        var found = await boards.GetByIdsAsync(distinct, cancellationToken);
        if (found.Count == distinct.Length)
        {
            return found;
        }

        var missing = distinct.Except(found.Select(b => b.Id)).ToArray();
        throw new RelatedEntitiesNotFoundException(nameof(OrderRequest.BoardIds), "board", missing);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static OrderDto ToDto(Order order) => new(
        order.Id,
        order.Name,
        order.Description,
        order.OrderDate,
        order.Boards.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).Select(b => new EntityRefDto(b.Id, b.Name)).ToList());
}
