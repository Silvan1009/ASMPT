using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Service.Api.Data.Entities;

namespace Service.Api.Data.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(o => o.Name);

        builder.Property(o => o.Description).HasMaxLength(2000);

        // OrderDate maps to the PostgreSQL "date" type by Npgsql convention.

        // Explicit, payload-free join entity: OrderId/BoardId are discovered by EF Core's foreign-key naming
        // convention. Both FKs are required, so deleting an order or a board only removes the join rows.
        builder.HasMany(o => o.Boards)
            .WithMany(b => b.Orders)
            .UsingEntity<OrderBoard>(j =>
            {
                j.ToTable("OrderBoards");
                j.HasKey(x => new { x.OrderId, x.BoardId });
            });
    }
}
