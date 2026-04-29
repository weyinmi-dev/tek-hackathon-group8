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
}
