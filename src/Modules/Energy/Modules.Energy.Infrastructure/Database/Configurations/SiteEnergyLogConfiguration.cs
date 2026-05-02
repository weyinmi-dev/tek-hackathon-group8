using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Energy.Domain.Telemetry;

namespace Modules.Energy.Infrastructure.Database.Configurations;

internal sealed class SiteEnergyLogConfiguration : IEntityTypeConfiguration<SiteEnergyLog>
{
    public void Configure(EntityTypeBuilder<SiteEnergyLog> builder)
    {
        builder.ToTable("site_energy_logs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.SiteCode).HasMaxLength(32).IsRequired();
        builder.HasIndex(l => new { l.SiteCode, l.RecordedAtUtc });
        builder.HasIndex(l => l.RecordedAtUtc);
        builder.Ignore(l => l.DomainEvents);
    }
}
