using Microsoft.EntityFrameworkCore;
using Modules.Ai.Application.Workflows;
using Modules.Ai.Infrastructure.Database;

namespace Modules.Ai.Infrastructure.Checkpointing;

/// <summary>
/// EF-backed implementation of the workflow-checkpoint persistence port. Stores each checkpoint
/// under a generated id in <c>ai.workflow_checkpoints</c>; the MAF adapter in Modules.Ai.Agents
/// treats that id as its opaque <c>CheckpointInfo.CheckpointId</c>.
/// </summary>
internal sealed class WorkflowCheckpointStore(AiDbContext db) : IWorkflowCheckpointStore
{
    public async Task<string> SaveAsync(string runId, string payloadJson, string? parentCheckpointId, CancellationToken cancellationToken = default)
    {
        string checkpointId = Guid.NewGuid().ToString("N");
        db.Set<WorkflowCheckpoint>().Add(new WorkflowCheckpoint
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            CheckpointId = checkpointId,
            ParentCheckpointId = parentCheckpointId,
            Payload = payloadJson,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        return checkpointId;
    }

    public async Task<string?> LoadAsync(string runId, string checkpointId, CancellationToken cancellationToken = default)
        => await db.Set<WorkflowCheckpoint>()
            .AsNoTracking()
            .Where(c => c.RunId == runId && c.CheckpointId == checkpointId)
            .Select(c => c.Payload)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<WorkflowCheckpointRef>> ListAsync(string runId, string? parentCheckpointId, CancellationToken cancellationToken = default)
    {
        IQueryable<WorkflowCheckpoint> query = db.Set<WorkflowCheckpoint>()
            .AsNoTracking()
            .Where(c => c.RunId == runId);

        if (parentCheckpointId is not null)
        {
            query = query.Where(c => c.ParentCheckpointId == parentCheckpointId);
        }

        return await query
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new WorkflowCheckpointRef(c.RunId, c.CheckpointId, c.ParentCheckpointId))
            .ToListAsync(cancellationToken);
    }
}
