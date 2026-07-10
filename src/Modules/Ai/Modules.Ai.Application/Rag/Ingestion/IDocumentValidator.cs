namespace Modules.Ai.Application.Rag.Ingestion;

/// <summary>
/// Determines if a document is relevant to the TelcoPilot mission (NOC, Energy, Network, etc.)
/// and should be allowed into the knowledge base.
/// </summary>
public interface IDocumentValidator
{
    /// <summary>
    /// Validates the document content.
    /// </summary>
    /// <returns>A tuple containing whether it is valid and the reason (rejection reason if false).</returns>
    Task<(bool IsValid, string Reason)> ValidateAsync(string fileName, string textPreview, CancellationToken ct = default);
}
