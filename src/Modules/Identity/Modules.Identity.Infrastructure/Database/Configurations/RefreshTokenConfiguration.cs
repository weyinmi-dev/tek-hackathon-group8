using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Identity.Domain.RefreshTokens;

namespace Modules.Identity.Infrastructure.Database.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.UserId);
        builder.Ignore(t => t.DomainEvents);
    }
}
