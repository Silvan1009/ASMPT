using Microsoft.EntityFrameworkCore;
using Service.Api.Data;
using Service.Api.Data.Entities;

namespace Service.Api.Repositories;

public sealed class OrderRepository(ApplicationDbContext dbContext) : Repository<Order>(dbContext), IOrderRepository
{
    public async Task<IReadOnlyList<Order>> SearchAsync(string? search, CancellationToken cancellationToken = default)
    {
        IQueryable<Order> query = Set.AsNoTracking().Include(o => o.Boards.OrderBy(b => b.Name));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePatterns.Contains(search.Trim());
            query = query.Where(o =>
                EF.Functions.ILike(o.Name, pattern, LikePatterns.Escape) ||
                (o.Description != null && EF.Functions.ILike(o.Description, pattern, LikePatterns.Escape)));
        }

        return await query.OrderBy(o => o.Name).ThenBy(o => o.Id).ToListAsync(cancellationToken);
    }

    public Task<Order?> GetWithBoardsAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.Include(o => o.Boards).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<Order?> GetForExportAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.AsNoTracking()
            .Include(o => o.Boards.OrderBy(b => b.Name))
            .ThenInclude(b => b.Components.OrderBy(c => c.Name))
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<int> DeleteByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => Set.Where(o => ids.Contains(o.Id)).ExecuteDeleteAsync(cancellationToken);
}
