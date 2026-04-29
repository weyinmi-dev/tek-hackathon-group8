using System.Text;
using Modules.Ai.Application.Rag.Ingestion;

namespace Modules.Ai.Infrastructure.Rag.Ingestion;

/// <summary>
/// Default text extractor for the demo: handles text/markdown directly and falls back to a
/// best-effort UTF-8 read for everything else. Swap in a Tika/PDF/Office adapter behind
/// <see cref="IDocumentTextExtractor"/> when richer source formats are needed —
/// the ingestion pipeline does not change.
/// </summary>
internal sealed class PlainTextDocumentExtractor : IDocumentTextExtractor
{
    public async Task<string> ExtractAsync(Stream content, string contentType, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
