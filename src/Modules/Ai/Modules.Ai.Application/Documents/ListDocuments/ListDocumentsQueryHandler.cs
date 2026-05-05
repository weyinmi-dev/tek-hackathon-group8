using Application.Abstractions.Messaging;
using Modules.Ai.Domain.Documents;
using SharedKernel;

namespace Modules.Ai.Application.Documents.ListDocuments;

internal sealed class ListDocumentsQueryHandler(IManagedDocumentRepository documents)
    : IQueryHandler<ListDocumentsQuery, PagedDocumentResult>
{
    public async Task<Result<PagedDocumentResult>> Handle(ListDocumentsQuery request, CancellationToken cancellationToken)
    {
        int totalCount = await documents.CountAsync(request.SearchTerm, cancellationToken);
        
        IReadOnlyList<ManagedDocument> pageItems = await documents.ListPagedAsync(request.Page, request.PageSize, request.SearchTerm, cancellationToken);
        
        IReadOnlyList<DocumentListItem> items = pageItems
            .Select(d => new DocumentListItem(
                d.Id, d.Title, d.FileName, d.SizeBytes, d.Category, d.Region, d.Tags,
                d.Source, d.Status.ToString(), d.Version, d.UploadedBy,
                d.UploadedAtUtc, d.IndexedAtUtc, d.LastIndexError, d.RejectionReason, d.ExternalReference))
            .ToList();
            
        return Result.Success(new PagedDocumentResult(items, totalCount));
    }
}
