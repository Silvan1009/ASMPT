using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Service.Api.Data.Entities;

namespace Service.Api.Data.Configurations;

public sealed class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.ToTable("Boards");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(b => b.Name);

        builder.Property(b => b.Description).HasMaxLength(2000);

        builder.Property(b => b.Length).HasPrecision(10, 2);
        builder.Property(b => b.Width).HasPrecision(10, 2);

        // Explicit, payload-free join entity: BoardId/ComponentId are discovered by EF Core's foreign-key
        // naming convention. Both FKs are required, so deleting a board or a component only removes the
        // join rows.
        builder.HasMany(b => b.Components)
            .WithMany(c => c.Boards)
            .UsingEntity<BoardComponent>(j =>
            {
                j.ToTable("BoardComponents");
                j.HasKey(x => new { x.BoardId, x.ComponentId });
            });

        // The Order side of the Order/Board relationship is configured once, in OrderConfiguration.
    }
}
