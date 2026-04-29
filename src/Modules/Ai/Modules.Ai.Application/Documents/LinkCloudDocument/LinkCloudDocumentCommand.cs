using Application.Abstractions.Messaging;
using Modules.Ai.Domain.Documents;
using Modules.Ai.Domain.Knowledge;

namespace Modules.Ai.Application.Documents.LinkCloudDocument;

/// <summary>
/// Cloud-link variant: the file already lives in a third-party drive (Google Drive,
/// OneDrive, SharePoint, Azure Blob). The caller supplies the provider's storage key
/// (Drive file ID, blob name, ...) and an optional public URL surfaced in the UI.
/// The bytes themselves are streamed on demand by the matching storage provider during
/// ingestion — we do not eagerly copy.
/// </summary>
public sealed record LinkCloudDocumentCommand(
    string Title,
    string FileName,
    string ContentType,
    long SizeBytes,
    KnowledgeCategory Category,
    string Region,
    IReadOnlyList<string> Tags,
    DocumentSource Source,
    string StorageKey,
    string? ExternalReference,
    string LinkedBy) : ICommand<LinkedDocumentDto>;

public sealed record LinkedDocumentDto(
    Guid Id,
    string Title,
    DocumentSource Source,
    string Status);
