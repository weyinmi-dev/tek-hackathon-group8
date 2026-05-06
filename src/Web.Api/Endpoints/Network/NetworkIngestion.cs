using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Identity.Application.Authorization;
using Modules.Network.Application.Ingestion.Pipeline;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Network;

/// <summary>
/// POST /api/network/ingest — multipart upload of a network-ops log file (csv/json/xlsx/txt).
/// Triggers the deterministic 5-stage pipeline. Returns the run summary; idempotent re-uploads
/// of the same content are short-circuited and return the original summary.
/// </summary>
public sealed class NetworkIngestion : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("network/ingest", [Authorize(Policy = Policies.RequireEngineer)]
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

            string actor = principal.FindFirstValue("handle") ?? "unknown";

            await using Stream content = file.OpenReadStream();
            Result<IngestionRunSummary> result = await sender.Send(new ProcessNetworkLogCommand(
                FileName: file.FileName,
                ContentType: file.ContentType ?? string.Empty,
                Content: content,
                SubmittedBy: actor), ct);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .DisableAntiforgery()
        .WithTags(Tags.NetworkIngestion);
    }
}
