namespace Modules.Ai.Application.Rag.Documents;

/// <summary>
/// Bound from <c>Ai:Documents:*</c>. Backs the <c>Local</c> document storage provider
/// and the future cloud-provider connection settings.
/// </summary>
public sealed class DocumentsOptions
{
    public const string SectionName = "Ai:Documents";

    /// <summary>
    /// Filesystem root the LocalDocumentStorageProvider writes uploads under.
    /// Mapped to a named Docker volume in compose so uploads survive container restarts.
    /// Relative paths are resolved against the process working directory.
    /// </summary>
    public string LocalRoot { get; init; } = "./.telcopilot/documents";

    /// <summary>
    /// Maximum upload size in bytes — guards memory use during ingestion.
    /// Default 25 MB; raise for SOPs with embedded diagrams.
    /// </summary>
    public long MaxUploadBytes { get; init; } = 25 * 1024 * 1024;
}
