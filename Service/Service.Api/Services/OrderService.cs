using System.Text.Json;
using Service.Api.Data.Entities;
using Service.Api.Models;
using Service.Api.Repositories;

namespace Service.Api.Services;

public sealed class OrderService(IOrderRepository orders, IBoardRepository boards, TimeProvider clock, ILogger<OrderService> logger) : IOrderService
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
            Name = RequestText.Required(request.Name),
            Description = RequestText.Optional(request.Description),
            OrderDate = OrderDateOf(request),
        };
        order.Boards.AddRange(await ResolveBoardsAsync(request.BoardIds, cancellationToken));

        await orders.AddAsync(order, cancellationToken);
        await orders.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Order {OrderId} created with {BoardCount} board(s)", order.Id, order.Boards.Count);
        return ToDto(order);
    }

    public async Task<OrderDto?> UpdateAsync(Guid id, OrderRequest request, CancellationToken cancellationToken = default)
    {
        var order = await orders.GetWithBoardsAsync(id, cancellationToken);
        if (order is null)
        {
            return null;
        }

        order.Name = RequestText.Required(request.Name);
        order.Description = RequestText.Optional(request.Description);
        order.OrderDate = OrderDateOf(request);

        // EF Core turns the boards added to / removed from the skip navigation into inserted / deleted
        // OrderBoards rows on SaveChanges.
        order.Boards.SyncTo(await ResolveBoardsAsync(request.BoardIds, cancellationToken));

        await orders.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Order {OrderId} updated", order.Id);
        return ToDto(order);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await orders.DeleteByIdsAsync([id], cancellationToken) == 1;
        if (deleted)
        {
            logger.LogInformation("Order {OrderId} deleted", id);
        }

        return deleted;
    }

    public async Task DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var deleted = await orders.DeleteByIdsAsync(ids, cancellationToken);
        logger.LogInformation("Deleted {Deleted} of {Requested} order(s)", deleted, ids.Count);
    }

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
            order.Boards.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).Select(b => new BoardExportDto(
                b.Id,
                b.Name,
                b.Description,
                b.Length,
                b.Width,
                b.Components.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(c => new ComponentExportDto(c.Id, c.Name, c.Description, c.Quantity))
                    .ToList())).ToList());

        var fileName = $"order-{FileNames.Sanitize(order.Name, order.Id.ToString())}.json";
        logger.LogInformation("Order {OrderId} exported", order.Id);
        return new OrderExportFile(fileName, JsonSerializer.SerializeToUtf8Bytes(export, ExportJsonOptions));
    }

    private Task<IReadOnlyList<Board>> ResolveBoardsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
        => RelatedEntities.ResolveAsync(ids, boards, nameof(OrderRequest.BoardIds), "board", cancellationToken);

    /// <summary>Model validation rejects a missing date before the action runs; the check documents the
    /// contract for callers that bypass MVC.</summary>
    private static DateOnly OrderDateOf(OrderRequest request)
        => request.OrderDate ?? throw new ArgumentException("OrderDate is required.", nameof(request));

    private static OrderDto ToDto(Order order) => new(
        order.Id,
        order.Name,
        order.Description,
        order.OrderDate,
        order.Boards.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase).Select(b => new EntityRefDto(b.Id, b.Name)).ToList());
}
