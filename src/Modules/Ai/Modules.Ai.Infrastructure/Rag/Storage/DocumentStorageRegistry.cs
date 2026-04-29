using System.Collections.ObjectModel;
using Modules.Ai.Application.Rag.Storage;
using Modules.Ai.Domain.Documents;

namespace Modules.Ai.Infrastructure.Rag.Storage;

internal sealed class DocumentStorageRegistry : IDocumentStorageRegistry
{
    private readonly Dictionary<DocumentSource, IDocumentStorageProvider> _providers;
    private readonly HashSet<DocumentSource> _live;

    public DocumentStorageRegistry(IEnumerable<IDocumentStorageProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Source);
        // A provider counts as "available" only if it's not a placeholder. Placeholders are
        // still registered so the system can return a descriptive error when called, but
        // they shouldn't appear as connectable options in the UI.
        _live = _providers
            .Where(kv => kv.Value is not Providers.PlaceholderCloudStorageProvider)
            .Select(kv => kv.Key)
            .ToHashSet();
    }

    public IDocumentStorageProvider For(DocumentSource source) =>
        _providers.TryGetValue(source, out IDocumentStorageProvider? p)
            ? p
            : throw new InvalidOperationException($"No storage provider registered for {source}.");

    public bool IsAvailable(DocumentSource source) => _live.Contains(source);

    public IReadOnlyCollection<DocumentSource> AvailableSources =>
        new ReadOnlyCollection<DocumentSource>([.. _live]);
}
