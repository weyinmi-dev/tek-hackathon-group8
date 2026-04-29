using Modules.Ai.Application.Rag.Storage;
using Modules.Ai.Domain.Documents;

namespace Modules.Ai.Infrastructure.Rag.Storage.Providers;

/// <summary>
/// Stand-in for a cloud-drive provider that has not been wired to a live SDK yet.
/// Concrete subclasses set <see cref="Source"/>; calls fail with a clear error message
/// instead of silently doing nothing — operators see exactly which provider needs
/// connecting in the document-management UI.
/// </summary>
internal abstract class PlaceholderCloudStorageProvider : IDocumentStorageProvider
{
    public abstract DocumentSource Source { get; }

    public Task<StoredObject> SaveAsync(string suggestedFileName, string contentType, Stream content, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) =>
        throw NotConfigured();

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    private InvalidOperationException NotConfigured() =>
        new($"{Source} document storage provider is registered as a placeholder. Wire a live SDK adapter in Modules.Ai.Infrastructure to enable {Source} ingestion.");
}

internal sealed class GoogleDriveDocumentStorageProvider : PlaceholderCloudStorageProvider
{
    public override DocumentSource Source => DocumentSource.GoogleDrive;
}

internal sealed class OneDriveDocumentStorageProvider : PlaceholderCloudStorageProvider
{
    public override DocumentSource Source => DocumentSource.OneDrive;
}

internal sealed class SharePointDocumentStorageProvider : PlaceholderCloudStorageProvider
{
    public override DocumentSource Source => DocumentSource.SharePoint;
}

internal sealed class AzureBlobDocumentStorageProvider : PlaceholderCloudStorageProvider
{
    public override DocumentSource Source => DocumentSource.AzureBlob;
}
