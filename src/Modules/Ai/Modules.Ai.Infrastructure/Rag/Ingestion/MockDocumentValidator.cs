using Modules.Ai.Application.Rag.Ingestion;

namespace Modules.Ai.Infrastructure.Rag.Ingestion;

/// <summary>
/// Pass-through validator used when Azure AI is not configured.
/// Accepts all documents so the RAG pipeline remains functional in local/dev environments.
/// </summary>
internal sealed class MockDocumentValidator : IDocumentValidator
{
    public Task<(bool IsValid, string Reason)> ValidateAsync(string fileName, string textPreview, CancellationToken ct = default)
        => Task.FromResult((true, "Mock validator: document accepted (AI validation disabled)."));
}
