using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag;
using Modules.Ai.Application.Rag.Embeddings;
using Modules.Ai.Application.Rag.Models;
using Modules.Ai.Application.Rag.Retrievers;
using Modules.Ai.Application.Rag.Stores;
using Modules.Ai.Domain.Knowledge;
using Pgvector;

namespace Modules.Ai.Infrastructure.Rag.Retrievers;

internal sealed class RagRetriever(
    IEmbeddingGenerator embeddings,
    IKnowledgeStore store,
    RagOptions options,
    ILogger<RagRetriever> logger) : IRagRetriever
{
    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(
        string query,
        int? topK = null,
        KnowledgeCategory? categoryFilter = null,
        string? regionFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        try
        {
            Vector embedded = await embeddings.GenerateAsync(query, cancellationToken);
            int k = topK ?? options.TopK;
            IReadOnlyList<RetrievedChunk> hits = await store.SearchAsync(embedded, k, categoryFilter, regionFilter, cancellationToken);
            logger.LogInformation("RAG retrieved {Count} chunks for query (model={Model}, topK={TopK})",
                hits.Count, embeddings.ModelName, k);
            return hits;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Retrieval is best-effort — never block the orchestrator on RAG failures.
            logger.LogWarning(ex, "RAG retrieval failed; orchestrator will proceed without context.");
            return [];
        }
    }
}
