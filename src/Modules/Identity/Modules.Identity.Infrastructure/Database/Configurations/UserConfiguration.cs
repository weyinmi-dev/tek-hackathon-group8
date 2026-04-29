using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Identity.Domain.Users;

namespace Modules.Identity.Infrastructure.Database.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(128).IsRequired();
        builder.Property(u => u.Handle).HasMaxLength(64).IsRequired();
        builder.Property(u => u.Role).HasMaxLength(32).IsRequired();
        builder.Property(u => u.Team).HasMaxLength(64).IsRequired();
        builder.Property(u => u.Region).HasMaxLength(64).IsRequired();
        builder.Property(u => u.IsActive).HasDefaultValue(true);

        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.Handle).IsUnique();

        builder.Ignore(u => u.DomainEvents);
    }
}
