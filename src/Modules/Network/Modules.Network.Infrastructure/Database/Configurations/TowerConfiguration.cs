using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Network.Domain.Towers;

namespace Modules.Network.Infrastructure.Database.Configurations;

internal sealed class TowerConfiguration : IEntityTypeConfiguration<Tower>
{
    public void Configure(EntityTypeBuilder<Tower> builder)
    {
        builder.ToTable("towers");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Code).HasMaxLength(32).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(128).IsRequired();
        builder.Property(t => t.Region).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Issue).HasMaxLength(256);
        builder.Property(t => t.Status).HasConversion<int>();
        builder.Property(t => t.ActivePowerSource).HasConversion<int>();
        builder.Property(t => t.FuelLevelLiters).HasDefaultValue(0);
        builder.Property(t => t.FuelCapacityLiters).HasDefaultValue(1000);
        builder.HasIndex(t => t.Code).IsUnique();
        builder.HasIndex(t => t.Region);
        builder.Ignore(t => t.DomainEvents);
    }
}
