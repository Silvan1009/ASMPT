using Microsoft.EntityFrameworkCore;
using Service.Api.Data;
using Service.Api.Data.Entities;

namespace Service.Api.Repositories;

public sealed class ComponentRepository(ApplicationDbContext dbContext) : Repository<Component>(dbContext), IComponentRepository
{
    public async Task<IReadOnlyList<Component>> SearchAsync(string? search, CancellationToken cancellationToken = default)
    {
        IQueryable<Component> query = Set.AsNoTracking().Include(c => c.Boards.OrderBy(b => b.Name));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = LikePatterns.Contains(search.Trim());
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, pattern, LikePatterns.Escape) ||
                (c.Description != null && EF.Functions.ILike(c.Description, pattern, LikePatterns.Escape)));
        }

        return await query.OrderBy(c => c.Name).ThenBy(c => c.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Component>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => await Set.Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken);

    public Task<Component?> GetWithBoardsAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.Include(c => c.Boards).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<int> DeleteByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => Set.Where(c => ids.Contains(c.Id)).ExecuteDeleteAsync(cancellationToken);
}
