namespace Modules.Ai.Domain.Documents;

/// <summary>
/// Where a managed document originated. Drives the storage provider that owns the
/// physical file and the ingestion pipeline that reads it.
/// Add new sources here as new providers come online — adding a value is a non-breaking
/// change for existing rows (the column is stored as int).
/// </summary>
public enum DocumentSource
{
    LocalUpload = 0,
    GoogleDrive = 1,
    OneDrive = 2,
    SharePoint = 3,
    AzureBlob = 4,
}
