using System.ComponentModel;
using MediatR;
using Modules.Ai.Application.Documents.ListDocuments;

namespace Modules.Ai.Agents.Tools;

/// <summary>
/// Document capability tool (Phase 2 §6.2). <c>search_documents</c> reuses the existing
/// <see cref="ListDocumentsQuery"/> rather than introducing a new query.
/// </summary>
public sealed class DocumentTools(ISender sender)
{
    [Description("Search the uploaded knowledge documents by title or content.")]
    public Task<string> SearchDocuments(
        [Description("Search text matched against document title and content.")] string search,
        CancellationToken cancellationToken = default)
        => ToolResult.DispatchAsync(
            sender,
            new ListDocumentsQuery(Page: 1, PageSize: 20, SearchTerm: string.IsNullOrWhiteSpace(search) ? null : search),
            cancellationToken);
}
