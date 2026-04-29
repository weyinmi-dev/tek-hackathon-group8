using Modules.Ai.Domain.Documents;

namespace Modules.Ai.Application.Rag.Storage;

/// <summary>
/// Resolves the right <see cref="IDocumentStorageProvider"/> for a given
/// <see cref="DocumentSource"/>. Registered providers self-declare which source they
/// own; the registry dispatches at runtime so the ingestion pipeline never branches
/// on provider identity.
/// </summary>
public interface IDocumentStorageRegistry
{
    /// <summary>Returns the provider for <paramref name="source"/>, or throws if none is registered.</summary>
    IDocumentStorageProvider For(DocumentSource source);

    /// <summary>True when a provider is registered for <paramref name="source"/>.</summary>
    bool IsAvailable(DocumentSource source);

    /// <summary>Sources currently advertised as available — drives the "where can I upload" picker in the UI.</summary>
    IReadOnlyCollection<DocumentSource> AvailableSources { get; }
}
