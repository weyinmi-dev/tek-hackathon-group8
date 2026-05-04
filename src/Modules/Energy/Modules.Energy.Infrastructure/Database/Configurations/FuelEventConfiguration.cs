using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Energy.Domain.Events;

namespace Modules.Energy.Infrastructure.Database.Configurations;

internal sealed class FuelEventConfiguration : IEntityTypeConfiguration<FuelEvent>
{
    public void Configure(EntityTypeBuilder<FuelEvent> builder)
    {
        builder.ToTable("fuel_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.SiteCode).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Kind).HasConversion<int>();
        builder.Property(e => e.Note).HasMaxLength(256);
        builder.HasIndex(e => new { e.SiteCode, e.OccurredAtUtc });
        builder.HasIndex(e => e.OccurredAtUtc);
        builder.Ignore(e => e.DomainEvents);
    }
}
