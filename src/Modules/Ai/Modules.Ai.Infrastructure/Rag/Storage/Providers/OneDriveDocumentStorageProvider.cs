using Modules.Ai.Application.Rag.Storage;
using Modules.Ai.Domain.Documents;

namespace Modules.Ai.Infrastructure.Rag.Storage.Providers;

/// <summary>
/// Fetches documents from Microsoft OneDrive sharing links.
/// </summary>
internal sealed class OneDriveDocumentStorageProvider(HttpClient httpClient) : IDocumentStorageProvider
{
    public DocumentSource Source => DocumentSource.OneDrive;

    public Task<StoredObject> SaveAsync(string suggestedFileName, string contentType, Stream content, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("OneDrive provider is read-only via public sharing links.");

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        // Simple heuristic: if it's a sharing link, try to convert it to a direct download
        string url = storageKey;
        if (url.Contains("onedrive.live.com"))
        {
            url = url.Replace("redir?", "download?")
                     .Replace("resid=", "resid="); // ensures it's a download link
        }

        return await httpClient.GetStreamAsync(url, cancellationToken);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("OneDrive provider is read-only.");

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
