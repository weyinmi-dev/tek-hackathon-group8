namespace Modules.Ai.Application.Workflows;

/// <summary>
/// Persistence port for Microsoft Agent Framework workflow checkpoints (Phase 2 §8, D6).
/// The MAF-facing <c>ICheckpointStore&lt;JsonElement&gt;</c> adapter lives in Modules.Ai.Agents and
/// delegates here, so the MAF dependency stays in the agent layer while persistence stays in
/// infrastructure. Payloads are opaque JSON. A checkpoint is identified by <c>(runId, checkpointId)</c>
/// with an optional parent, forming the lineage MAF walks when resuming a workflow.
/// </summary>
public interface IWorkflowCheckpointStore
{
    /// <summary>Persists a checkpoint payload for a run and returns the generated checkpoint id.</summary>
    Task<string> SaveAsync(string runId, string payloadJson, string? parentCheckpointId, CancellationToken cancellationToken = default);

    /// <summary>Loads a checkpoint payload by id, or <c>null</c> if it does not exist.</summary>
    Task<string?> LoadAsync(string runId, string checkpointId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists checkpoint references for a run, oldest first. When <paramref name="parentCheckpointId"/>
    /// is supplied, only the checkpoints whose parent matches it are returned.
    /// </summary>
    Task<IReadOnlyList<WorkflowCheckpointRef>> ListAsync(string runId, string? parentCheckpointId, CancellationToken cancellationToken = default);
}

/// <summary>Lightweight reference to a stored checkpoint (no payload).</summary>
public sealed record WorkflowCheckpointRef(string RunId, string CheckpointId, string? ParentCheckpointId);
