using System.Text;
using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag.Ingestion;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Modules.Ai.Infrastructure.Rag.Ingestion;

/// <summary>
/// Default text extractor used by the RAG ingestion pipeline. Dispatches on the
/// uploaded document's content type / file extension:
///   - application/pdf (or *.pdf) -> PdfPig text layer
///   - text/* (or *.txt|.md|.csv|.json|.log) -> UTF-8 read
///   - everything else -> rejected with a clear message so the document is marked
///     Failed instead of indexing binary garbage as if it were text.
///
/// The previous implementation read every upload as UTF-8 regardless of type,
/// which silently turned PDFs into a stream of replacement characters and either
/// indexed garbage or stranded the document at InProgress.
/// </summary>
internal sealed class DefaultDocumentTextExtractor(ILogger<DefaultDocumentTextExtractor> logger) : IDocumentTextExtractor
{
    private static readonly HashSet<string> TextLikeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".csv", ".json", ".log", ".yaml", ".yml", ".html", ".htm", ".xml",
    };

    public async Task<string> ExtractAsync(Stream content, string contentType, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        string type = (contentType ?? string.Empty).Trim();
        string name = fileName ?? string.Empty;
        string extension = Path.GetExtension(name);

        if (IsPdf(type, extension))
        {
            return await ExtractPdfAsync(content, name, cancellationToken);
        }

        if (IsTextLike(type, extension))
        {
            return await ExtractTextAsync(content, cancellationToken);
        }

        // Unsupported binary format. Throw with a clear message so the ingestion
        // pipeline records it via MarkFailed and the user sees actionable feedback
        // in the documents page instead of the document staying at InProgress.
        throw new NotSupportedException(
            $"Content type '{contentType}' (file '{fileName}') is not supported for text extraction. " +
            "Supported types: PDF, plain text, markdown, JSON, CSV, log, HTML, XML, YAML.");
    }

    private static bool IsPdf(string contentType, string extension) =>
        string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
        || string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);

    private static bool IsTextLike(string contentType, string extension) =>
        contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
        || string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase)
        || string.Equals(contentType, "application/xml", StringComparison.OrdinalIgnoreCase)
        || string.Equals(contentType, "application/x-yaml", StringComparison.OrdinalIgnoreCase)
        || TextLikeExtensions.Contains(extension);

    private async Task<string> ExtractPdfAsync(Stream content, string fileName, CancellationToken cancellationToken)
    {
        // PdfPig doesn't take a CancellationToken; copy into a MemoryStream first so we
        // honour cancellation during the (potentially large) network/disk read, then run
        // the synchronous parse. PDFs that are scanned-image-only legitimately produce no
        // text — the caller's empty-body guard will mark those Failed.
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var sb = new StringBuilder();
        try
        {
            using var pdf = PdfDocument.Open(buffer);
            int pageCount = 0;
            foreach (Page page in pdf.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                string pageText = page.Text;
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    sb.AppendLine(pageText);
                    sb.AppendLine();
                }
                pageCount++;
            }
            logger.LogInformation("PDF '{FileName}' extracted: {PageCount} pages, {CharCount} chars",
                fileName, pageCount, sb.Length);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // PdfPig surfaces parsing problems as generic exceptions. Wrap with context
            // so the operator sees "PDF parse failed: ..." instead of a raw stack trace
            // bubbled up from a third-party library.
            throw new InvalidOperationException($"Failed to parse PDF '{fileName}': {ex.Message}", ex);
        }

        return sb.ToString();
    }

    private static async Task<string> ExtractTextAsync(Stream content, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
