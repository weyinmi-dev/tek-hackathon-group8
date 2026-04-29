using Application.Abstractions.Messaging;
using Modules.Ai.Domain.Documents;
using SharedKernel;

namespace Modules.Ai.Application.Documents.ListDocuments;

internal sealed class ListDocumentsQueryHandler(IManagedDocumentRepository documents)
    : IQueryHandler<ListDocumentsQuery, IReadOnlyList<DocumentListItem>>
{
    public async Task<Result<IReadOnlyList<DocumentListItem>>> Handle(ListDocumentsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<ManagedDocument> all = await documents.ListAsync(cancellationToken);
        IReadOnlyList<DocumentListItem> items = all
            .Select(d => new DocumentListItem(
                d.Id, d.Title, d.FileName, d.SizeBytes, d.Category, d.Region, d.Tags,
                d.Source, d.Status.ToString(), d.Version, d.UploadedBy,
                d.UploadedAtUtc, d.IndexedAtUtc, d.LastIndexError, d.ExternalReference))
            .ToList();
        return Result.Success(items);
    }
}
