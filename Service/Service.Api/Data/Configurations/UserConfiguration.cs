using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Service.Api.Data.Entities;

namespace Service.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FirebaseUid).HasMaxLength(128).IsRequired();
        builder.HasIndex(u => u.FirebaseUid).IsUnique();

        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(256);
        builder.Property(u => u.Role).HasMaxLength(64);

        // CreatedAt / LastLoginAt map to timestamptz. Npgsql only accepts DateTimeOffset values with a zero
        // offset, so callers must use TimeProvider.GetUtcNow().
    }
}
