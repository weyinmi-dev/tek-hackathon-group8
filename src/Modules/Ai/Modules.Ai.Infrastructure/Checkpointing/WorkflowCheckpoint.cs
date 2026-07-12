namespace Modules.Ai.Infrastructure.Checkpointing;

/// <summary>
/// A persisted MAF workflow checkpoint row (Phase 2 §8, D6) in the <c>ai</c> schema. Written at
/// each workflow superstep boundary so a restarted run resumes from the last one instead of
/// re-processing from the top. Nothing writes rows until workflows run (Phase 3 M7); until then
/// the table stays empty.
/// </summary>
internal sealed class WorkflowCheckpoint
{
    public Guid Id { get; init; }

    /// <summary>The MAF workflow run this checkpoint belongs to.</summary>
    public string RunId { get; init; } = null!;

    /// <summary>Store-generated id, the <c>CheckpointId</c> half of MAF's opaque handle.</summary>
    public string CheckpointId { get; init; } = null!;

    /// <summary>Parent checkpoint id, forming the resume lineage. Null for a root checkpoint.</summary>
    public string? ParentCheckpointId { get; init; }

    /// <summary>Opaque JSON payload MAF serialized for this checkpoint.</summary>
    public string Payload { get; init; } = null!;

    public DateTime CreatedAtUtc { get; init; }
}
