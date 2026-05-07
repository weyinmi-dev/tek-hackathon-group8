using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Network.Domain.Ingestion;

namespace Modules.Network.Infrastructure.Database.Configurations;

internal sealed class IngestionRunConfiguration : IEntityTypeConfiguration<IngestionRun>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public void Configure(EntityTypeBuilder<IngestionRun> builder)
    {
        builder.ToTable("ingestion_runs");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(r => r.FileName).HasMaxLength(256).IsRequired();
        builder.Property(r => r.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(r => r.SubmittedBy).HasMaxLength(128).IsRequired();
        builder.Property(r => r.FailureReason).HasMaxLength(1024);
        builder.Property(r => r.Status).HasConversion<int>();

        builder.HasIndex(r => r.ContentHash).IsUnique();
        builder.HasIndex(r => r.StartedAt);
        builder.HasIndex(r => r.Status);

        builder.Property<List<StageTiming>>("_stageTimings")
            .HasField("_stageTimings")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("stage_timings")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v ?? new List<StageTiming>(), JsonOptions),
                v => string.IsNullOrWhiteSpace(v)
                    ? new List<StageTiming>()
                    : JsonSerializer.Deserialize<List<StageTiming>>(v, JsonOptions) ?? new List<StageTiming>());

        builder.Ignore(r => r.StageTimings);
        builder.Ignore(r => r.DomainEvents);
    }
}
