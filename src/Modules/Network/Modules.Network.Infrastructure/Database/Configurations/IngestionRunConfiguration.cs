using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Network.Domain.Ingestion;

namespace Modules.Network.Infrastructure.Database.Configurations;

internal sealed class IngestionRunConfiguration : IEntityTypeConfiguration<IngestionRun>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Tells EF how to detect that one of these JSON-backed lists actually changed.
    ///
    /// Without a comparer, EF snapshots a converted property by <b>reference</b>. Both lists are
    /// mutated in place on an already-tracked run — StageTiming rows are appended as each stage
    /// finishes, and the change list is filled on Complete() — so the reference never changes, EF
    /// concludes nothing happened, and the UPDATE omits the column entirely. The row is INSERTed with
    /// an empty list at bootstrap and stays that way: every completed run in the database had
    /// <c>stage_timings = []</c>, and the sync-history page's stage timings were always blank. The
    /// upload modal appeared to work only because it renders the in-memory response, never a re-read.
    ///
    /// Snapshotting deep-clones the list so EF compares the state at load against the state at save,
    /// which is the only way an in-place mutation can be seen.
    /// </summary>
    private static ValueComparer<List<T>> ListComparer<T>() =>
        new(
            (a, b) => (a ?? new List<T>()).SequenceEqual(b ?? new List<T>()),
            v => v == null ? 0 : v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item!.GetHashCode())),
            v => v == null ? new List<T>() : v.ToList());

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
                    : JsonSerializer.Deserialize<List<StageTiming>>(v, JsonOptions) ?? new List<StageTiming>())
            .Metadata.SetValueComparer(ListComparer<StageTiming>());

        // The itemised change list. Same shape as stage_timings: read only as a whole, alongside its
        // run, never queried across runs — so a JSON column rather than a child table.
        builder.Property<List<SyncChange>>("_changes")
            .HasField("_changes")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("changes")
            .HasColumnType("jsonb")

            // Nullable on purpose. The schema reconciler adds a missing column only if it can give
            // existing rows a value, so it skips required reference types outright — a NOT NULL
            // jsonb column would simply never be created on a database that already has this table,
            // and every read of it would fail with "column changes does not exist". The converter
            // reads null back as an empty list, so nullable costs the domain nothing.
            .IsRequired(false)

            .HasConversion(
                v => JsonSerializer.Serialize(v ?? new List<SyncChange>(), JsonOptions),
                v => string.IsNullOrWhiteSpace(v)
                    ? new List<SyncChange>()
                    : JsonSerializer.Deserialize<List<SyncChange>>(v, JsonOptions) ?? new List<SyncChange>())
            .Metadata.SetValueComparer(ListComparer<SyncChange>());

        builder.Ignore(r => r.Changes);
        builder.Ignore(r => r.StageTimings);
        builder.Ignore(r => r.DomainEvents);
    }
}
