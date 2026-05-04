using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Energy.Domain.Sites;

namespace Modules.Energy.Infrastructure.Database.Configurations;

internal sealed class BatteryHealthConfiguration : IEntityTypeConfiguration<BatteryHealth>
{
    public void Configure(EntityTypeBuilder<BatteryHealth> builder)
    {
        builder.ToTable("battery_health");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.SiteCode).HasMaxLength(32).IsRequired();
        builder.HasIndex(b => b.SiteCode).IsUnique();
        builder.Ignore(b => b.DomainEvents);
    }
}
