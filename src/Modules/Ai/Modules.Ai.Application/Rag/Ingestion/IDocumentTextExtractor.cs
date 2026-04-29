namespace Modules.Ai.Application.Rag.Ingestion;

/// <summary>
/// Pluggable text extractor — turns raw bytes from a storage provider into the plain
/// text the chunker can split. Default implementation supports text/markdown directly
/// and falls back to a best-effort UTF-8 read for unknown content types so the
/// architecture works end-to-end without a PDF/Office parser yet.
/// </summary>
public interface IDocumentTextExtractor
{
    Task<string> ExtractAsync(Stream content, string contentType, string fileName, CancellationToken cancellationToken = default);
}
