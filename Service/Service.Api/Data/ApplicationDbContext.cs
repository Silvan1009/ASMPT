using Microsoft.EntityFrameworkCore;
using Service.Api.Data.Entities;

namespace Service.Api.Data;

/// <summary>
/// EF Core context for the service, backed by PostgreSQL. Entity mappings live in
/// <c>Data/Configurations</c> and are applied by <see cref="OnModelCreating"/>.
/// </summary>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Board> Boards => Set<Board>();

    public DbSet<Component> Components => Set<Component>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
}
