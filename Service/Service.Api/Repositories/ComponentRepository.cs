using Microsoft.EntityFrameworkCore;
using Service.Api.Data;
using Service.Api.Data.Entities;

namespace Service.Api.Repositories;

public sealed class ComponentRepository(ApplicationDbContext dbContext) : EntityRepository<Component>(dbContext), IComponentRepository
{
    public async Task<IReadOnlyList<Component>> SearchAsync(string? search, CancellationToken cancellationToken = default)
        => await Set.AsNoTracking()
            .Include(c => c.Boards)
            .WhereNameOrDescriptionContains(search)
            .OrderBy(c => c.Name).ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);

    public Task<Component?> GetWithBoardsAsync(Guid id, CancellationToken cancellationToken = default)
        => Set.Include(c => c.Boards).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
}
