namespace Modules.Ai.Domain.Documents;

/// <summary>
/// Where a managed document sits in the ingestion pipeline. Drives the document-management
/// UI badge and lets the ingestion job pick up where it left off after a restart.
/// </summary>
public enum IndexingStatus
{
    Pending = 0,
    InProgress = 1,
    Indexed = 2,
    Failed = 3,
    Rejected = 4,

    /// <summary>
    /// Ingestion was deliberately stopped — operator aborted the upload, or in-flight work
    /// was drained on shutdown. Distinct from <see cref="Failed"/>: cancellation is not an error.
    /// Added for the asynchronous document workflow (Phase 2 §9.5).
    /// </summary>
    Cancelled = 5,
}
