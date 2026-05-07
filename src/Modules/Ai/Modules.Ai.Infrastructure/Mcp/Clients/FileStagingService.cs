using Application.Abstractions.Storage;
using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag.Documents;

namespace Modules.Ai.Infrastructure.Mcp.Clients;

/// <summary>
/// Persists uploaded files under the telcopilot root so they are accessible to the
/// <c>@modelcontextprotocol/server-filesystem</c> MCP server (FS plugin) and to the
/// Stage-2 analyzer for raw-content enrichment of SK prompts.
///
/// Root is derived from DocumentsOptions.LocalRoot by stepping up one directory level:
/// e.g. "./.telcopilot/documents" → "./.telcopilot". This keeps uploads co-located with
/// the documents folder that the MCP server is already rooted at.
/// </summary>
internal sealed class FileStagingService(
    DocumentsOptions documents,
    ILogger<FileStagingService> logger) : IFileStagingService
{
    private const int MaxReadChars = 3000;

    private readonly string _root = ResolveRoot(documents.LocalRoot);

    public string Root => _root;

    public async Task<string?> StageAsync(
        string contentHash,
        string fileName,
        byte[] bytes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string prefix = contentHash.Length >= 8 ? contentHash[..8] : contentHash;
            string dir = Path.Combine(_root, "uploads", prefix);
            Directory.CreateDirectory(dir);

            string safeName = SanitizeFileName(fileName);
            string fullPath = Path.Combine(dir, safeName);

            if (!File.Exists(fullPath))
            {
                await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
            }

            // Forward-slash path so MCP tools (which run on Node.js) receive a clean path.
            return Path.GetRelativePath(_root, fullPath).Replace('\\', '/');
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FileStagingService: failed to stage {FileName}", fileName);
            return null;
        }
    }

    public async Task<string?> TryReadTextAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));

            // Traversal guard
            if (!fullPath.StartsWith(_root, StringComparison.Ordinal))
            {
                return null;
            }

            if (!File.Exists(fullPath))
            {
                return null;
            }

            string text = await File.ReadAllTextAsync(fullPath, cancellationToken);
            return text.Length > MaxReadChars ? text[..MaxReadChars] : text;
        }
        catch
        {
            // Binary files, encoding errors, etc. — not an error, just no raw context.
            return null;
        }
    }

    // Derive telcopilot root from the documents sub-directory path.
    private static string ResolveRoot(string documentsLocalRoot)
    {
        string docs = string.IsNullOrWhiteSpace(documentsLocalRoot)
            ? "./.telcopilot/documents"
            : documentsLocalRoot;
        return Path.GetFullPath(Path.Combine(docs, ".."));
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "upload.bin";
        char[] invalid = Path.GetInvalidFileNameChars();
        var clean = new System.Text.StringBuilder(fileName.Length);
        foreach (char c in fileName)
            clean.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        return clean.ToString();
    }
}
