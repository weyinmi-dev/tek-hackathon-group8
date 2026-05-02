using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Energy.Domain.Sites;

namespace Modules.Energy.Infrastructure.Database.Configurations;

internal sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("sites");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Code).HasMaxLength(32).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(128).IsRequired();
        builder.Property(s => s.Region).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Source).HasConversion<int>();
        builder.Property(s => s.Health).HasConversion<int>();
        builder.Property(s => s.AnomalyNote).HasMaxLength(256);
        builder.HasIndex(s => s.Code).IsUnique();
        builder.HasIndex(s => s.Region);
        builder.Ignore(s => s.DomainEvents);
    }
}
