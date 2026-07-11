using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Ai.Application.Documents.DeleteDocument;
using Modules.Ai.Application.Documents.LinkCloudDocument;
using Modules.Ai.Application.Documents.ListDocuments;
using Modules.Ai.Application.Documents.ReindexDocument;
using Modules.Ai.Application.Documents.SyncDocuments;
using Modules.Ai.Application.Documents.UploadDocument;
using Application.Abstractions.Storage;
using Modules.Ai.Domain.Documents;
using Modules.Ai.Domain.Knowledge;
using Modules.Identity.Application.Authorization;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Documents;

public sealed class Documents : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/documents → engineer+ can browse the indexed corpus
        app.MapGet("documents", [Authorize(Policy = Policies.RequireEngineer)]
            async (int? page, int? pageSize, string? search, ISender sender, CancellationToken ct) =>
        {
            int p = page is > 0 ? page.Value : 1;
            int ps = pageSize is > 0 ? pageSize.Value : 10;
            Result<PagedDocumentResult> result = await sender.Send(new ListDocumentsQuery(p, ps, search), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Documents);

        // GET /api/documents/providers → which storage providers are wired up live vs placeholder
        app.MapGet("documents/providers", [Authorize(Policy = Policies.RequireEngineer)]
            (IDocumentStorageRegistry registry) =>
        {
            DocumentSource[] all = Enum.GetValues<DocumentSource>();
            object[] providers = all.Select(s => new
            {
                source = s.ToString(),
                value = (int)s,
                isAvailable = registry.IsAvailable(s),
            }).ToArray<object>();
            return Results.Ok(providers);
        })
        .WithTags(Tags.Documents);

        // POST /api/documents/upload → manager+ may upload local documents.
        app.MapPost("documents/upload", [Authorize(Policy = Policies.RequireManager)]
            async (HttpRequest http, ClaimsPrincipal principal, ISender sender, CancellationToken ct) =>
        {
            if (!http.HasFormContentType)
            {
                return Results.Problem("Expected multipart/form-data upload.", statusCode: 400);
            }

            IFormCollection form = await http.ReadFormAsync(ct);
            IFormFile? file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                return Results.Problem("No file uploaded.", statusCode: 400);
            }

            string title = Form(form, "title", file.FileName);
            string region = Form(form, "region", "All regions");
            string tagsRaw = Form(form, "tags", string.Empty);
            string categoryStr = Form(form, "category", KnowledgeCategory.EngineeringSop.ToString());
            KnowledgeCategory category = ParseCategory(categoryStr) ?? KnowledgeCategory.EngineeringSop;

            string actor = principal.FindFirstValue("handle") ?? "unknown";

            await using Stream content = file.OpenReadStream();
            Result<UploadedDocumentDto> result = await sender.Send(new UploadDocumentCommand(
                Title: title,
                FileName: file.FileName,
                ContentType: file.ContentType,
                Content: content,
                SizeBytes: file.Length,
                Category: category,
                Region: region,
                Tags: SplitTags(tagsRaw),
                UploadedBy: actor), ct);

            return result.Match(v => Results.Created($"/api/documents/{v.Id}", v), CustomResults.Problem);
        })
        .WithTags(Tags.Documents)
        .DisableAntiforgery();

        // POST /api/documents/link → manager+ may link a document already living in a cloud drive.
        app.MapPost("documents/link", [Authorize(Policy = Policies.RequireManager)]
            async (LinkRequest body, ClaimsPrincipal principal, ISender sender, CancellationToken ct) =>
        {
            if (!Enum.TryParse<DocumentSource>(body.Source, ignoreCase: true, out DocumentSource source) ||
                source == DocumentSource.LocalUpload)
            {
                return Results.Problem("Source must be one of: GoogleDrive, OneDrive, SharePoint, AzureBlob.", statusCode: 400);
            }

            KnowledgeCategory category = ParseCategory(body.Category) ?? KnowledgeCategory.EngineeringSop;
            string actor = principal.FindFirstValue("handle") ?? "unknown";

            Result<LinkedDocumentDto> result = await sender.Send(new LinkCloudDocumentCommand(
                Title: body.Title,
                FileName: body.FileName,
                ContentType: string.IsNullOrWhiteSpace(body.ContentType) ? "application/octet-stream" : body.ContentType,
                SizeBytes: body.SizeBytes,
                Category: category,
                Region: body.Region ?? "All regions",
                Tags: SplitTags(body.Tags ?? string.Empty),
                Source: source,
                StorageKey: body.StorageKey,
                ExternalReference: body.ExternalReference,
                LinkedBy: actor), ct);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Documents);

        // POST /api/documents/{id}/reindex → manager+ may retry indexing.
        app.MapPost("documents/{id:guid}/reindex", [Authorize(Policy = Policies.RequireManager)]
            async (Guid id, ISender sender, CancellationToken ct) =>
        {
            Result result = await sender.Send(new ReindexDocumentCommand(id), ct);
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Documents);

        // DELETE /api/documents/{id} → admin only.
        app.MapDelete("documents/{id:guid}", [Authorize(Policy = Policies.RequireEngineer)]
            async (Guid id, ISender sender, CancellationToken ct) =>
        {
            Result result = await sender.Send(new DeleteDocumentCommand(id), ct);
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Documents);
        
        // GET /api/documents/{id}/download → engineer+ can retrieve the original file.
        app.MapGet("documents/{id:guid}/download", [Authorize(Policy = Policies.RequireEngineer)]
            async (Guid id, IManagedDocumentRepository documents, IDocumentStorageRegistry storage, CancellationToken ct) =>
        {
            ManagedDocument? doc = await documents.GetByIdAsync(id, ct);
            if (doc is null) return Results.NotFound();
            
            if (!storage.IsAvailable(doc.Source))
            {
                return Results.Problem($"Storage provider {doc.Source} is not connected.", statusCode: 400);
            }
            
            try
            {
                IDocumentStorageProvider provider = storage.For(doc.Source);
                Stream stream = await provider.OpenReadAsync(doc.StorageKey, ct);
                return Results.File(stream, doc.ContentType, doc.FileName);
            }
            catch (Exception ex)
            {
                return Results.Problem($"Failed to retrieve file from {doc.Source}: {ex.Message}");
            }
        })
        .WithTags(Tags.Documents);

        // POST /api/documents/sync → admin only. Triggers manual discovery on all seeders.
        app.MapPost("documents/sync", [Authorize(Policy = Policies.RequireAdmin)]
            async (ISender sender, CancellationToken ct) =>
        {
            Result result = await sender.Send(new SyncDocumentsCommand(), ct);
            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags(Tags.Documents);
    }

    private static string Form(IFormCollection form, string key, string fallback) =>
        form.TryGetValue(key, out Microsoft.Extensions.Primitives.StringValues v) && !string.IsNullOrWhiteSpace(v) ? v.ToString() : fallback;

    private static IReadOnlyList<string> SplitTags(string raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? Array.Empty<string>()
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static KnowledgeCategory? ParseCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return null;
        }
        string normalized = category.Trim().Replace("_", "", StringComparison.Ordinal);
        return Enum.TryParse<KnowledgeCategory>(normalized, ignoreCase: true, out KnowledgeCategory parsed)
            ? parsed
            : null;
    }

    public sealed record LinkRequest(
        string Title,
        string FileName,
        string ContentType,
        long SizeBytes,
        string? Region,
        string? Tags,
        string Category,
        string Source,
        string StorageKey,
        string? ExternalReference);
}
