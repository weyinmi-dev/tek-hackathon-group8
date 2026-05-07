using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Network.Domain.Ingestion;

namespace Modules.Network.Infrastructure.Database.Configurations;

internal sealed class NetworkEventConfiguration : IEntityTypeConfiguration<NetworkEvent>
{
    public void Configure(EntityTypeBuilder<NetworkEvent> builder)
    {
        builder.ToTable("network_events");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TowerCode).HasMaxLength(32).IsRequired();
        builder.Property(e => e.RawStatus).HasMaxLength(64);
        builder.Property(e => e.RawPayload).HasColumnType("jsonb");

        builder.HasIndex(e => e.IngestionRunId);
        builder.HasIndex(e => new { e.TowerCode, e.OccurredAt });

        builder.HasOne<IngestionRun>()
            .WithMany()
            .HasForeignKey(e => e.IngestionRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(e => e.DomainEvents);
    }
}
