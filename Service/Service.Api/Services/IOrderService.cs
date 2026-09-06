using Service.Api.Models;

namespace Service.Api.Services;

public interface IOrderService
{
    Task<IReadOnlyList<OrderDto>> SearchAsync(string? search, CancellationToken cancellationToken = default);

    /// <summary>Null when no such order exists.</summary>
    Task<OrderDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<OrderDto> CreateAsync(OrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>Null when no such order exists.</summary>
    Task<OrderDto?> UpdateAsync(Guid id, OrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>False when no such order exists.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes several orders at once; unknown ids are ignored.</summary>
    Task DeleteManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Renders the order (with its boards and their components) as a downloadable file: the
    /// simulated hand-off to a production line. Null when no such order exists.</summary>
    Task<OrderExportFile?> ExportAsync(Guid id, CancellationToken cancellationToken = default);
}
