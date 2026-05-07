using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Alerts.Domain.Alerts;

namespace Modules.Alerts.Infrastructure.Database.Configurations;

internal sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Code).HasMaxLength(32).IsRequired();
        builder.Property(a => a.Title).HasMaxLength(256).IsRequired();
        builder.Property(a => a.Region).HasMaxLength(64).IsRequired();
        builder.Property(a => a.TowerCode).HasMaxLength(64).IsRequired();
        builder.Property(a => a.AiCause).HasMaxLength(512).IsRequired();
        builder.Property(a => a.AcknowledgedBy).HasMaxLength(64);
        builder.Property(a => a.AssignedTeam).HasMaxLength(64);
        builder.Property(a => a.AssignedBy).HasMaxLength(64);
        builder.Property(a => a.DispatchTarget).HasMaxLength(128);
        builder.Property(a => a.DispatchedBy).HasMaxLength(64);
        builder.Property(a => a.Severity).HasConversion<int>();
        builder.Property(a => a.Status).HasConversion<int>();

        // Stage-4 dedup. Nullable so the auto-EnsureCreated migration helper (which only
        // adds nullable columns) can roll the new shape into existing alerts tables.
        builder.Property(a => a.AnomalyFingerprint).HasMaxLength(64);

        builder.HasIndex(a => a.Code).IsUnique();
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.RaisedAtUtc);
        builder.HasIndex(a => a.AnomalyFingerprint);
        builder.Ignore(a => a.DomainEvents);
    }
}
