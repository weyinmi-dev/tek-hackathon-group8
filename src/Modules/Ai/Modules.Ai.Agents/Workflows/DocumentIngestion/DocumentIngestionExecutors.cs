using MediatR;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Modules.Ai.Application.Ingestion;
using Modules.Ai.Application.Rag.Chunking;
using Modules.Ai.Application.Rag.Embeddings;
using Modules.Ai.Application.Rag.Ingestion;
using Application.Abstractions.Storage;
using Pgvector;
using SharedKernel;

namespace Modules.Ai.Agents.Workflows.DocumentIngestion;

// The seven executors of DocumentIngestionWorkflow. Each is stateless: it receives a message, does
// exactly one step, and returns the next message. Repository/state work goes through ISender
// commands so the Agents layer never references a repository (Phase 2 §4.1); the pure compute
// steps (extract, chunk, embed) and the intake agent are injected directly. The conditional
// branches (validate → chunk|reject, extract → validate|fail) are carried by the workflow edges,
// not by the executors — see DocumentIngestionWorkflowBuilder.

/// <summary>Step 1 — read the stored file and extract its text.</summary>
public sealed partial class ExtractTextExecutor(
    IDocumentStorageRegistry storage,
    IDocumentTextExtractor extractor) : Executor("extract-text")
{
    [MessageHandler]
    public async ValueTask<ExtractedText> HandleAsync(IngestDocumentRequest doc, IWorkflowContext context)
    {
        IDocumentStorageProvider provider = storage.For(doc.Source);
        await using Stream stream = await provider.OpenReadAsync(doc.StorageKey);
        string text = await extractor.ExtractAsync(stream, doc.ContentType, doc.FileName);
        return new ExtractedText(doc, !string.IsNullOrWhiteSpace(text), text);
    }
}

/// <summary>Step 2 — the AI quality gate. Ask the intake agent whether the document is relevant.</summary>
public sealed partial class ValidateRelevanceExecutor(AIAgent intakeAgent) : Executor("validate-relevance")
{
    [MessageHandler]
    public async ValueTask<ValidationResult> HandleAsync(ExtractedText input, IWorkflowContext context)
    {
        string preview = input.Text.Length > 2000 ? input.Text[..2000] : input.Text;
        AgentResponse response = await intakeAgent.RunAsync(
            $"File: {input.Doc.FileName}\nCategory: {input.Doc.Category}\n\n{preview}");
        string verdict = response.ToString() ?? string.Empty;

        // "IRRELEVANT" contains "RELEVANT" as a substring, so the negative check must gate the positive.
        bool relevant = verdict.Contains("RELEVANT", StringComparison.OrdinalIgnoreCase)
            && !verdict.Contains("IRRELEVANT", StringComparison.OrdinalIgnoreCase);
        string reason = relevant
            ? "relevant"
            : (verdict.Length > 0 ? verdict.Trim() : "rejected by the intake agent");
        return new ValidationResult(input.Doc, input.Text, relevant, reason);
    }
}

/// <summary>Step 3 — split the text into retrieval-sized windows.</summary>
public sealed partial class ChunkTextExecutor(IChunker chunker) : Executor("chunk-text")
{
    [MessageHandler]
    public ValueTask<ChunkedText> HandleAsync(ValidationResult input, IWorkflowContext context)
        => ValueTask.FromResult(new ChunkedText(input.Doc, input.Text, chunker.Split(input.Text)));
}

/// <summary>Step 4 — embed the chunks. Its own superstep so a resume after it never re-embeds.</summary>
public sealed partial class EmbedChunksExecutor(IEmbeddingGenerator embeddings) : Executor("embed-chunks")
{
    // Azure OpenAI caps a single embeddings request at 2048 inputs; partition to stay under it.
    private const int MaxBatch = 2048;

    [MessageHandler]
    public async ValueTask<EmbeddedText> HandleAsync(ChunkedText input, IWorkflowContext context)
    {
        var vectors = new List<float[]>(input.Chunks.Count);
        for (int offset = 0; offset < input.Chunks.Count; offset += MaxBatch)
        {
            var batch = input.Chunks
                .Skip(offset)
                .Take(MaxBatch)
                .Select(c => c.Content)
                .ToList();
            IReadOnlyList<Vector> embedded = await embeddings.GenerateBatchAsync(batch);
            vectors.AddRange(embedded.Select(v => v.ToArray()));
        }

        return new EmbeddedText(input.Doc, input.Text, input.Chunks, vectors);
    }
}

/// <summary>Step 5 (accept terminal) — persist the knowledge document + chunks and mark Indexed.</summary>
public sealed partial class PersistKnowledgeExecutor(ISender sender) : Executor("persist-knowledge")
{
    [MessageHandler]
    public async ValueTask<IngestionCompleted> HandleAsync(EmbeddedText input, IWorkflowContext context)
    {
        var chunks = input.Chunks
            .Zip(input.Vectors, (c, v) => new ChunkEmbedding(c.Ordinal, c.Content, c.TokenEstimate, v))
            .ToList();

        var command = new PersistDocumentKnowledgeCommand(
            input.Doc.DocumentId,
            input.Doc.SourceKey,
            input.Doc.Category,
            input.Doc.Title,
            input.Doc.Region,
            input.Doc.Tags,
            input.Text,
            input.Doc.OccurredAtUtc,
            chunks);

        Result<int> result = await sender.Send(command);
        return new IngestionCompleted(
            input.Doc.DocumentId,
            result.IsSuccess ? "Indexed" : "Failed",
            result.IsSuccess ? result.Value : 0);
    }
}

/// <summary>Reject terminal — the intake agent judged the document irrelevant.</summary>
public sealed partial class RejectDocumentExecutor(ISender sender) : Executor("reject-document")
{
    [MessageHandler]
    public async ValueTask<IngestionCompleted> HandleAsync(ValidationResult input, IWorkflowContext context)
    {
        await sender.Send(new MarkDocumentRejectedCommand(input.Doc.DocumentId, input.Reason));
        return new IngestionCompleted(input.Doc.DocumentId, "Rejected", 0);
    }
}

/// <summary>Fail terminal — extraction produced no text.</summary>
public sealed partial class FailDocumentExecutor(ISender sender) : Executor("fail-document")
{
    [MessageHandler]
    public async ValueTask<IngestionCompleted> HandleAsync(ExtractedText input, IWorkflowContext context)
    {
        await sender.Send(new MarkDocumentFailedCommand(
            input.Doc.DocumentId,
            "Extractor returned empty text — the document may be a scanned image or have no extractable text layer."));
        return new IngestionCompleted(input.Doc.DocumentId, "Failed", 0);
    }
}
