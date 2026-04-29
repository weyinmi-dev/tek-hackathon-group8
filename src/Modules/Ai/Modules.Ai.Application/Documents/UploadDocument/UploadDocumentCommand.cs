using Application.Abstractions.Messaging;
using Modules.Ai.Domain.Documents;
using Modules.Ai.Domain.Knowledge;

namespace Modules.Ai.Application.Documents.UploadDocument;

/// <summary>
/// Local-upload variant: caller hands over the raw bytes (already loaded into a stream)
/// plus metadata. The handler persists via the configured local storage provider and
/// kicks off the ingestion pipeline.
/// </summary>
public sealed record UploadDocumentCommand(
    string Title,
    string FileName,
    string ContentType,
    Stream Content,
    long SizeBytes,
    KnowledgeCategory Category,
    string Region,
    IReadOnlyList<string> Tags,
    string UploadedBy) : ICommand<UploadedDocumentDto>;

public sealed record UploadedDocumentDto(
    Guid Id,
    string Title,
    string FileName,
    long SizeBytes,
    string Status,
    DocumentSource Source);
