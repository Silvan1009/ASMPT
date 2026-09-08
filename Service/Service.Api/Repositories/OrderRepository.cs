using Microsoft.EntityFrameworkCore;
using Service.Api.Data;
using Service.Api.Data.Entities;

namespace Service.Api.Repositories;

public sealed class OrderRepository(ApplicationDbContext dbContext) : EntityRepository<Order>(dbContext), IOrderRepository
{
    public async Task<IReadOnlyList<Order>> SearchAsync(string? search, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Include(o => o.Boards)
            .WhereNameOrDescriptionContains(search)
            .OrderBy(o => o.Name).ThenBy(o => o.Id)
            .ToListAsync(cancellationToken);

    public Task<Order?> GetWithBoardsAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.Include(o => o.Boards).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<Order?> GetForExportAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.AsNoTracking()
            .Include(o => o.Boards)
            .ThenInclude(b => b.Components)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
}
