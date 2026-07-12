using MediatR;
using Microsoft.Agents.AI.Workflows;
using Modules.Ai.Agents.Agents;
using Modules.Ai.Application.Rag.Chunking;
using Modules.Ai.Application.Rag.Embeddings;
using Modules.Ai.Application.Rag.Ingestion;
using Application.Abstractions.Storage;

namespace Modules.Ai.Agents.Workflows.DocumentIngestion;

/// <summary>
/// Builds DocumentIngestionWorkflow (Phase 2 §7.1): the async, resumable replacement for the
/// synchronous DocumentIngestionService. The graph is a linear chain with two data-driven splits —
///
///   extract ──[has text]──▶ validate ──[relevant]──▶ chunk ─▶ embed ─▶ persist   (Indexed)
///        │                        │
///        └──[no text]──▶ fail     └──[irrelevant]──▶ reject                       (Failed / Rejected)
///
/// The split predicates live on the edges (AddEdge(from, to, Func&lt;msg,bool&gt;)), so exactly one
/// downstream executor receives each message even though both branch executors accept the same type.
/// Each arrow is a superstep boundary where MAF checkpoints the in-flight message, which is what lets
/// a crashed run resume from the last completed step (verified against the M5 Postgres store at M9).
/// </summary>
public sealed class DocumentIngestionWorkflowBuilder(
    IDocumentStorageRegistry storage,
    IDocumentTextExtractor extractor,
    DocumentIntakeAgentBuilder intakeAgentBuilder,
    IChunker chunker,
    IEmbeddingGenerator embeddings,
    ISender sender)
{
    public Workflow Build()
    {
        var extract = new ExtractTextExecutor(storage, extractor);
        var validate = new ValidateRelevanceExecutor(intakeAgentBuilder.Build());
        var chunk = new ChunkTextExecutor(chunker);
        var embed = new EmbedChunksExecutor(embeddings);
        var persist = new PersistKnowledgeExecutor(sender);
        var reject = new RejectDocumentExecutor(sender);
        var fail = new FailDocumentExecutor(sender);

        return new WorkflowBuilder(extract)
            .AddEdge(extract, validate, (ExtractedText m) => m.HasText)
            .AddEdge(extract, fail, (ExtractedText m) => !m.HasText)
            .AddEdge(validate, chunk, (ValidationResult m) => m.Relevant)
            .AddEdge(validate, reject, (ValidationResult m) => !m.Relevant)
            .AddEdge(chunk, embed)
            .AddEdge(embed, persist)
            .WithOutputFrom(new ExecutorBinding[] { persist, reject, fail })
            .Build();
    }
}
