namespace Application.Abstractions.Storage;

/// <summary>
/// Stages binary payloads under the telcopilot working directory so they are
/// accessible to the filesystem MCP server (FS plugin) and to the Stage-2 AI
/// analyzer for raw-content enrichment.
/// </summary>
public interface IFileStagingService
{
    /// <summary>Absolute path of the telcopilot root directory.</summary>
    string Root { get; }

    /// <summary>
    /// Writes <paramref name="bytes"/> to
    /// <c>{Root}/uploads/{contentHash[..8]}/{fileName}</c>. Returns the path
    /// relative to <see cref="Root"/> (forward-slash separated) so MCP tools
    /// can reference it via <c>FS.read_file</c>. Idempotent — does not overwrite
    /// an existing file with the same hash prefix. Returns <c>null</c> on I/O error.
    /// </summary>
    Task<string?> StageAsync(
        string contentHash,
        string fileName,
        byte[] bytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the text content of a file at <paramref name="relativePath"/> within
    /// <see cref="Root"/>. Returns <c>null</c> when the file does not exist,
    /// is outside the root (traversal guard), or cannot be decoded as UTF-8 text.
    /// Caps the return value at 3000 characters to keep SK prompt tokens bounded.
    /// </summary>
    Task<string?> TryReadTextAsync(
        string relativePath,
        CancellationToken cancellationToken = default);
}
