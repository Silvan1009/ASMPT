using Microsoft.EntityFrameworkCore;
using Service.Api.Data;
using Service.Api.Data.Entities;

namespace Service.Api.Repositories;

public sealed class BoardRepository(ApplicationDbContext dbContext) : Repository<Board>(dbContext), IBoardRepository
{
    public async Task<IReadOnlyList<Board>> SearchAsync(string? search, CancellationToken cancellationToken = default)
    {
        IQueryable<Board> query = Set.AsNoTracking()
            .Include(b => b.Components.OrderBy(c => c.Name))
            .Include(b => b.Orders.OrderBy(o => o.Name));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePatterns.Contains(search.Trim());
            query = query.Where(b =>
                EF.Functions.ILike(b.Name, pattern, LikePatterns.Escape) ||
                (b.Description != null && EF.Functions.ILike(b.Description, pattern, LikePatterns.Escape)));
        }

        return await query.OrderBy(b => b.Name).ThenBy(b => b.Id).ToListAsync(cancellationToken);
    }

    public Task<Board?> GetWithComponentsAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.Include(b => b.Components).FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Board>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => await Set.Where(b => ids.Contains(b.Id)).ToListAsync(cancellationToken);

    public Task<int> DeleteByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => Set.Where(b => ids.Contains(b.Id)).ExecuteDeleteAsync(cancellationToken);
}
