using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Domain.Optimizations;

namespace Modules.Network.Infrastructure.Database.Configurations;

internal sealed class OptimizationConfiguration : IEntityTypeConfiguration<Optimization>
{
    public void Configure(EntityTypeBuilder<Optimization> builder)
    {
        builder.ToTable("optimizations");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.TowerCode).HasMaxLength(32).IsRequired();
        builder.Property(o => o.AnomalyFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(o => o.Rationale).HasMaxLength(1024).IsRequired();
        builder.Property(o => o.Type).HasConversion<int>();

        builder.HasIndex(o => o.IngestionRunId);
        builder.HasIndex(o => new { o.TowerCode, o.ProposedAt });
        builder.HasIndex(o => o.AnomalyFingerprint);

        builder.HasOne<IngestionRun>()
            .WithMany()
            .HasForeignKey(o => o.IngestionRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(o => o.DomainEvents);
    }
}
