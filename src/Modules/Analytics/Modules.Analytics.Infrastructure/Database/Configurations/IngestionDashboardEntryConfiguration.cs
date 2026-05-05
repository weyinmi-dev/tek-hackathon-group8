using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Analytics.Domain.Ingestion;

namespace Modules.Analytics.Infrastructure.Database.Configurations;

internal sealed class IngestionDashboardEntryConfiguration : IEntityTypeConfiguration<IngestionDashboardEntry>
{
    public void Configure(EntityTypeBuilder<IngestionDashboardEntry> builder)
    {
        builder.ToTable("ingestion_dashboard_entries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(e => e.FileName).HasMaxLength(256).IsRequired();

        builder.HasIndex(e => e.IngestionRunId).IsUnique();
        builder.HasIndex(e => e.CompletedAt);

        builder.Ignore(e => e.DomainEvents);
    }
}
