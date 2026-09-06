using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Service.Api.Data.Entities;

namespace Service.Api.Data.Configurations;

public sealed class ComponentConfiguration : IEntityTypeConfiguration<Component>
{
    public void Configure(EntityTypeBuilder<Component> builder)
    {
        builder.ToTable("Components");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.Name);

        builder.Property(c => c.Description).HasMaxLength(2000);

        // The Board side of the Board/Component relationship is configured once, in BoardConfiguration.
    }
}
