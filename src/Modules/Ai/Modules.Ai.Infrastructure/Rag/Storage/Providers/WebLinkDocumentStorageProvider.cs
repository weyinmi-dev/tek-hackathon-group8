using Modules.Ai.Application.Rag.Storage;
using Modules.Ai.Domain.Documents;

namespace Modules.Ai.Infrastructure.Rag.Storage.Providers;

/// <summary>
/// Fetches documents from any public web URL.
/// </summary>
internal sealed class WebLinkDocumentStorageProvider(HttpClient httpClient) : IDocumentStorageProvider
{
    public DocumentSource Source => DocumentSource.WebLink;

    public Task<StoredObject> SaveAsync(string suggestedFileName, string contentType, Stream content, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("WebLink provider is read-only.");

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        return await httpClient.GetStreamAsync(storageKey, cancellationToken);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("WebLink provider is read-only.");

    public async Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, storageKey), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
