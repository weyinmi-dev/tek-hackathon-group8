using Modules.Ai.Application.Rag.Models;

namespace Modules.Ai.Application.Rag.Ingestion;

/// <summary>
/// Orchestrates the read-side of the RAG pipeline:
/// fetch bytes from the source provider → extract text → hand off to <c>IRagIndexer</c>.
/// Idempotent on the underlying ManagedDocument.
/// </summary>
public interface IDocumentIngestionService
{
    Task<IndexResult> IngestAsync(Guid managedDocumentId, CancellationToken cancellationToken = default);
}
