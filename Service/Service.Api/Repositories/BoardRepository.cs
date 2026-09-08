using Microsoft.EntityFrameworkCore;
using Service.Api.Data;
using Service.Api.Data.Entities;

namespace Service.Api.Repositories;

public sealed class BoardRepository(ApplicationDbContext dbContext) : EntityRepository<Board>(dbContext), IBoardRepository
{
    // Two sibling collection Includes: AsSplitQuery keeps EF Core from joining components x orders into one
    // cartesian result set per board.
    public async Task<IReadOnlyList<Board>> SearchAsync(string? search, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Include(b => b.Components)
            .Include(b => b.Orders)
            .AsSplitQuery()
            .WhereNameOrDescriptionContains(search)
            .OrderBy(b => b.Name).ThenBy(b => b.Id)
            .ToListAsync(cancellationToken);

    public Task<Board?> GetWithRelationsAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.Include(b => b.Components)
            .Include(b => b.Orders)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
}
