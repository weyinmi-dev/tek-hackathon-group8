namespace Modules.Ai.Application.Rag.Storage;

/// <summary>
/// Provider-agnostic descriptor returned after a write completes. <c>StorageKey</c>
/// is the provider's internal handle (relative path / blob name / Drive file ID);
/// <c>ExternalReference</c> is an optional human-readable URL or pointer surfaced
/// in the document-management UI.
/// </summary>
public sealed record StoredObject(
    string StorageKey,
    long SizeBytes,
    string ContentType,
    string? ExternalReference);
