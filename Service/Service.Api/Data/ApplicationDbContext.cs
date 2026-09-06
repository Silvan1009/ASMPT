using Microsoft.EntityFrameworkCore;

namespace Service.Api.Data;

/// <summary>
/// EF Core context for the service, backed by PostgreSQL. No entities are registered yet;
/// add <c>DbSet&lt;T&gt;</c> properties here as persisted features are introduced.
/// </summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
}
