using SharedKernel;
using Application.Abstractions.Messaging;
using Modules.Ai.Domain.Documents;
using Modules.Ai.Domain.Knowledge;

namespace Modules.Ai.Application.Documents.ListDocuments;

public sealed record ListDocumentsQuery(int Page = 1, int PageSize = 10, string? SearchTerm = null) : IQuery<PagedDocumentResult>;

public sealed record PagedDocumentResult(IReadOnlyList<DocumentListItem> Items, int TotalCount);


public sealed record DocumentListItem(
    Guid Id,
    string Title,
    string FileName,
    long SizeBytes,
    KnowledgeCategory Category,
    string Region,
    string Tags,
    DocumentSource Source,
    string Status,
    int Version,
    string UploadedBy,
    DateTime UploadedAtUtc,
    DateTime? IndexedAtUtc,
    string? LastIndexError,
    string? RejectionReason,
    string? ExternalReference);
