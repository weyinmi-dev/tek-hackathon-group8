using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Energy.Domain.Events;

namespace Modules.Energy.Infrastructure.Database.Configurations;

internal sealed class AnomalyEventConfiguration : IEntityTypeConfiguration<AnomalyEvent>
{
    public void Configure(EntityTypeBuilder<AnomalyEvent> builder)
    {
        builder.ToTable("anomalies");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.SiteCode).HasMaxLength(32).IsRequired();
        builder.Property(a => a.Kind).HasConversion<int>();
        builder.Property(a => a.Severity).HasConversion<int>();
        builder.Property(a => a.Detail).HasMaxLength(512).IsRequired();
        builder.Property(a => a.ModelName).HasMaxLength(64).IsRequired();
        builder.Property(a => a.AcknowledgedBy).HasMaxLength(64);

        // Nullable and NOT unique: seeded and ML-detected rows carry no key, and a condition that
        // clears and later returns keeps the same key across both the closed and reopened row.
        builder.Property(a => a.DetectionKey).HasMaxLength(128);
        builder.HasIndex(a => a.DetectionKey);

        builder.HasIndex(a => a.SiteCode);
        builder.HasIndex(a => new { a.DetectedAtUtc, a.Acknowledged });
        builder.Ignore(a => a.DomainEvents);
    }
}
