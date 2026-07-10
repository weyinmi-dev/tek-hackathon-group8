using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Ai.Infrastructure.Checkpointing;

namespace Modules.Ai.Infrastructure.Database.Configurations;

internal sealed class WorkflowCheckpointConfiguration : IEntityTypeConfiguration<WorkflowCheckpoint>
{
    public void Configure(EntityTypeBuilder<WorkflowCheckpoint> b)
    {
        b.ToTable("workflow_checkpoints");
        b.HasKey(c => c.Id);
        b.Property(c => c.RunId).HasMaxLength(128).IsRequired();
        b.Property(c => c.CheckpointId).HasMaxLength(64).IsRequired();
        b.Property(c => c.ParentCheckpointId).HasMaxLength(64);
        b.Property(c => c.Payload).IsRequired();
        b.Property(c => c.CreatedAtUtc).IsRequired();

        // Look-up by handle; index the run for index/list queries.
        b.HasIndex(c => new { c.RunId, c.CheckpointId }).IsUnique();
        b.HasIndex(c => c.RunId);
    }
}
