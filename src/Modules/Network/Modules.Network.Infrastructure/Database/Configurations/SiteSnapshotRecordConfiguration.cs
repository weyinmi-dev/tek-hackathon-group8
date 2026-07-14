using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Network.Domain.Ingestion;

namespace Modules.Network.Infrastructure.Database.Configurations;

internal sealed class SiteSnapshotRecordConfiguration : IEntityTypeConfiguration<SiteSnapshotRecord>
{
    public void Configure(EntityTypeBuilder<SiteSnapshotRecord> builder)
    {
        builder.ToTable("site_snapshots");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.RequestId).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Provider).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Environment).HasMaxLength(32).IsRequired();
        builder.Property(s => s.SiteId).HasMaxLength(64).IsRequired();
        builder.Property(s => s.SiteCode).HasMaxLength(32).IsRequired();
        builder.Property(s => s.SiteName).HasMaxLength(128).IsRequired();
        builder.Property(s => s.Region).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Vendor).HasMaxLength(64);
        builder.Property(s => s.Technologies).HasMaxLength(64).IsRequired();
        builder.Property(s => s.RawJson).HasColumnType("jsonb").IsRequired();

        // The telemetry access pattern: "give me this site's history, newest first". Every trend
        // chart and the site-details view hit exactly this index.
        builder.HasIndex(s => new { s.SiteCode, s.CapturedAt });

        // The file-index access patterns: by run (the sync report) and by provider (the uploads list).
        builder.HasIndex(s => s.IngestionRunId);
        builder.HasIndex(s => s.Provider);

        builder.HasOne<IngestionRun>()
            .WithMany()
            .HasForeignKey(s => s.IngestionRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(s => s.DomainEvents);
    }
}
