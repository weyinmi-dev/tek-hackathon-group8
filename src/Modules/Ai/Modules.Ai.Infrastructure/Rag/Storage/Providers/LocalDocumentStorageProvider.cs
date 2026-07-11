using Modules.Ai.Application.Rag.Documents;
using Modules.Ai.Application.Rag.Storage;
using Modules.Ai.Domain.Documents;

namespace Modules.Ai.Infrastructure.Rag.Storage.Providers;

/// <summary>
/// Stores uploaded documents on the host filesystem under <see cref="DocumentsOptions.LocalRoot"/>.
/// In Docker that root is mapped to a named volume so files survive container restarts;
/// in Aspire it defaults to <c>./.telcopilot/documents</c> next to the AppHost working dir.
/// </summary>
internal sealed class LocalDocumentStorageProvider(DocumentsOptions options) : IDocumentStorageProvider
{
    private readonly string _root = ResolveRoot(options.LocalRoot);
    // Reads fall back across the watch folders too: the seeder registers admin-provided documents
    // (in AdditionalWatchFolders) with the bare file name as their storage key, so resolving a key
    // against LocalRoot alone would fail to find them. Writes still target LocalRoot only.
    private readonly IReadOnlyList<string> _readRoots = BuildReadRoots(options);

    public DocumentSource Source => DocumentSource.LocalUpload;

    public async Task<StoredObject> SaveAsync(string suggestedFileName, string contentType, Stream content, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);

        string safeName = SanitizeFileName(suggestedFileName);
        string storageKey = $"{Guid.NewGuid():N}-{safeName}";
        string fullPath = Path.Combine(_root, storageKey);

        await using FileStream output = File.Create(fullPath);
        await content.CopyToAsync(output, cancellationToken);
        await output.FlushAsync(cancellationToken);

        long size = output.Length;
        return new StoredObject(storageKey, size, contentType, ExternalReference: null);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        string fullPath = ResolvePath(storageKey);
        Stream stream = File.OpenRead(fullPath);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        string fullPath = ResolvePath(storageKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(ResolvePath(storageKey)));

    private string ResolvePath(string storageKey)
    {
        // Return the first root that actually holds the file (LocalRoot, then watch folders), so
        // admin-provided documents resolve. Each candidate is traversal-guarded against its own root.
        foreach (string root in _readRoots)
        {
            string candidate = Path.GetFullPath(Path.Combine(root, storageKey));
            if (!candidate.StartsWith(root, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Storage key escapes the configured root.");
            }
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // Not found anywhere — return the LocalRoot path so callers get a consistent
        // FileNotFound rather than a misleading watch-folder path. (Traversal was already
        // rejected above, since LocalRoot is the first root checked.)
        return Path.GetFullPath(Path.Combine(_root, storageKey));
    }

    private static IReadOnlyList<string> BuildReadRoots(DocumentsOptions options)
    {
        var roots = new List<string> { ResolveRoot(options.LocalRoot) };
        foreach (string folder in options.AdditionalWatchFolders ?? [])
        {
            if (!string.IsNullOrWhiteSpace(folder))
            {
                roots.Add(ResolveRoot(folder));
            }
        }
        return roots;
    }

    private static string ResolveRoot(string configured)
    {
        string root = string.IsNullOrWhiteSpace(configured) ? "./.telcopilot/documents" : configured;
        return Path.GetFullPath(root);
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "upload.bin";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        var clean = new System.Text.StringBuilder(fileName.Length);
        foreach (char c in fileName)
        {
            clean.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }
        return clean.ToString();
    }
}
