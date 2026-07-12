using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Modules.Ai.Application.Workflows;

namespace Modules.Ai.Agents.Workflows;

/// <summary>
/// Durable MAF checkpoint store backed by Postgres (Phase 2 D6). Implements the framework's
/// <see cref="ICheckpointStore{TStoreObject}"/> over the <see cref="IWorkflowCheckpointStore"/>
/// persistence port, so the MAF dependency stays in this agent layer while the actual storage
/// lives in infrastructure. Wired into a <c>CheckpointManager.CreateJson(...)</c> when workflows
/// start running (Phase 3 M7).
///
/// MAF treats <see cref="CheckpointInfo"/> as an opaque handle, so the payload is round-tripped
/// verbatim: <see cref="CreateCheckpointAsync"/> stores the raw JSON under a generated id, and
/// <see cref="RetrieveCheckpointAsync"/> looks it up by that id.
/// </summary>
public sealed class PostgresCheckpointStore(IWorkflowCheckpointStore store) : ICheckpointStore<JsonElement>
{
    public async ValueTask<CheckpointInfo> CreateCheckpointAsync(string runId, JsonElement value, CheckpointInfo? parent)
    {
        string checkpointId = await store.SaveAsync(runId, value.GetRawText(), parent?.CheckpointId);
        return new CheckpointInfo(runId, checkpointId);
    }

    public async ValueTask<JsonElement> RetrieveCheckpointAsync(string runId, CheckpointInfo key)
    {
        string? payload = await store.LoadAsync(runId, key.CheckpointId)
            ?? throw new InvalidOperationException($"Checkpoint '{key.CheckpointId}' not found for run '{runId}'.");

        using JsonDocument document = JsonDocument.Parse(payload);
        return document.RootElement.Clone();
    }

    public async ValueTask<IEnumerable<CheckpointInfo>> RetrieveIndexAsync(string runId, CheckpointInfo? withParent)
    {
        IReadOnlyList<WorkflowCheckpointRef> refs = await store.ListAsync(runId, withParent?.CheckpointId);
        return refs.Select(r => new CheckpointInfo(r.RunId, r.CheckpointId)).ToList();
    }
}
