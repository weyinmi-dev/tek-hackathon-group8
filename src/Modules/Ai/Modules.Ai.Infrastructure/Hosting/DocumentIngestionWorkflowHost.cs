using Application.Abstractions.Events;
using MediatR;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Logging;
using Modules.Ai.Agents.Workflows;
using Modules.Ai.Agents.Workflows.DocumentIngestion;
using Modules.Ai.Application.Workflows;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Documents;

namespace Modules.Ai.Infrastructure.Hosting;

/// <summary>
/// Runs <c>DocumentIngestionWorkflow</c> in response to <see cref="DocumentUploaded"/>. This is the
/// durability seam (Phase 2 D6): the workflow definition never mentions checkpoints; the host binds
/// the checkpoint manager to the Postgres store, so every superstep is persisted and a crash mid-run
/// resumes from the last one rather than starting over.
/// </summary>
/// <remarks>
/// Registered explicitly in DI (MediatR scans the Application assembly, not Infrastructure). It runs
/// inside the outbox publish scope, so it is already off the request thread — the upload has long
/// since returned 202.
/// </remarks>
internal sealed class DocumentIngestionWorkflowHost(
    DocumentIngestionWorkflowBuilder workflowBuilder,
    IManagedDocumentRepository documents,
    IUnitOfWork uow,
    IWorkflowCheckpointStore checkpointStore,
    ILogger<DocumentIngestionWorkflowHost> logger) : INotificationHandler<DocumentUploaded>
{
    public async Task Handle(DocumentUploaded notification, CancellationToken cancellationToken)
    {
        ManagedDocument? doc = await documents.GetByIdAsync(notification.DocumentId, cancellationToken);
        if (doc is null)
        {
            logger.LogWarning("DocumentUploaded for unknown document {DocumentId} — ignoring.", notification.DocumentId);
            return;
        }

        // Deterministic run id keyed to the document, so a restart after a crash finds the same run
        // and resumes it instead of re-ingesting from scratch.
        string runId = $"doc-ingest-{doc.Id:N}";
        var manager = CheckpointManager.CreateJson(new PostgresCheckpointStore(checkpointStore));
        Workflow workflow = workflowBuilder.Build();

        IReadOnlyList<WorkflowCheckpointRef> existing = await checkpointStore.ListAsync(runId, null, cancellationToken);

        StreamingRun run;
        if (existing.Count > 0)
        {
            // A prior attempt left checkpoints (the process died mid-workflow). Resume from the most
            // recent one — ListAsync orders by creation time, so the last entry is the leaf.
            var latest = new CheckpointInfo(runId, existing[^1].CheckpointId);
            logger.LogInformation(
                "Resuming DocumentIngestionWorkflow for {DocumentId} from checkpoint {CheckpointId}.",
                doc.Id, latest.CheckpointId);
            run = await InProcessExecution.ResumeStreamingAsync(workflow, latest, manager, cancellationToken);
        }
        else
        {
            doc.MarkInProgress();
            await uow.SaveChangesAsync(cancellationToken);

            var request = new IngestDocumentRequest(
                doc.Id, doc.Source, doc.StorageKey, doc.ContentType, doc.FileName,
                doc.Title, doc.Region, doc.Tags, doc.Category, doc.UploadedAtUtc, doc.Version);
            run = await InProcessExecution.RunStreamingAsync(
                workflow, request, manager, sessionId: runId, cancellationToken: cancellationToken);
        }

        await foreach (WorkflowEvent evt in run.WatchStreamAsync(cancellationToken))
        {
            if (evt is WorkflowOutputEvent { Data: IngestionCompleted outcome })
            {
                logger.LogInformation(
                    "DocumentIngestionWorkflow finished for {DocumentId}: {Status} ({ChunkCount} chunks).",
                    outcome.DocumentId, outcome.Status, outcome.ChunkCount);
            }
        }
    }
}
