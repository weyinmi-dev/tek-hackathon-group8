using Modules.Ai.Domain.Knowledge;
using SharedKernel;

namespace Modules.Ai.Domain.Documents;

/// <summary>
/// Tracks an uploaded or cloud-linked source document, independently of its
/// chunked/embedded form (<see cref="KnowledgeDocument"/>). One ManagedDocument
/// may produce one KnowledgeDocument once the ingestion pipeline succeeds.
/// </summary>
public sealed class ManagedDocument : Entity
{
    private ManagedDocument(
        Guid id,
        string title,
        string fileName,
        string contentType,
        long sizeBytes,
        KnowledgeCategory category,
        string region,
        string tags,
        DocumentSource source,
        string storageKey,
        string? externalReference,
        string uploadedBy,
        int version) : base(id)
    {
        Title = title;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Category = category;
        Region = region;
        Tags = tags;
        Source = source;
        StorageKey = storageKey;
        ExternalReference = externalReference;
        UploadedBy = uploadedBy;
        UploadedAtUtc = DateTime.UtcNow;
        Status = IndexingStatus.Pending;
        Version = version;
    }

    private ManagedDocument() { }

    public string Title { get; private set; } = null!;
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public KnowledgeCategory Category { get; private set; }
    public string Region { get; private set; } = null!;
    public string Tags { get; private set; } = null!;
    public DocumentSource Source { get; private set; }

    /// <summary>Provider-internal handle used to locate the bytes (path / blob name / Google file ID).</summary>
    public string StorageKey { get; private set; } = null!;

    /// <summary>Optional public reference (e.g. Google Drive URL, SharePoint ID) shown in the UI.</summary>
    public string? ExternalReference { get; private set; }

    public string UploadedBy { get; private set; } = null!;
    public DateTime UploadedAtUtc { get; private set; }
    public IndexingStatus Status { get; private set; }
    public DateTime? IndexedAtUtc { get; private set; }
    public string? LastIndexError { get; private set; }
    public int Version { get; private set; }
    public Guid? KnowledgeDocumentId { get; private set; }

    public static ManagedDocument Create(
        string title,
        string fileName,
        string contentType,
        long sizeBytes,
        KnowledgeCategory category,
        string region,
        IReadOnlyList<string> tags,
        DocumentSource source,
        string storageKey,
        string? externalReference,
        string uploadedBy)
    {
        return new ManagedDocument(
            Guid.NewGuid(),
            string.IsNullOrWhiteSpace(title) ? fileName : title.Trim(),
            fileName,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            sizeBytes,
            category,
            region ?? string.Empty,
            string.Join(',', tags ?? []),
            source,
            storageKey,
            externalReference,
            uploadedBy,
            version: 1);
    }

    public void MarkInProgress()
    {
        Status = IndexingStatus.InProgress;
        LastIndexError = null;
    }

    public void MarkIndexed(Guid knowledgeDocumentId)
    {
        Status = IndexingStatus.Indexed;
        IndexedAtUtc = DateTime.UtcNow;
        KnowledgeDocumentId = knowledgeDocumentId;
        LastIndexError = null;
    }

    public void MarkFailed(string error)
    {
        Status = IndexingStatus.Failed;
        LastIndexError = string.IsNullOrWhiteSpace(error) ? "Unknown failure" : error;
    }

    public void RecordNewVersion(string storageKey, long sizeBytes, string contentType, string? externalReference)
    {
        Version++;
        StorageKey = storageKey;
        SizeBytes = sizeBytes;
        ContentType = string.IsNullOrWhiteSpace(contentType) ? ContentType : contentType;
        ExternalReference = externalReference;
        Status = IndexingStatus.Pending;
        IndexedAtUtc = null;
        LastIndexError = null;
    }
}
