using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Energy.Application.Sites.GetSiteTrace;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Energy;

public sealed class GetSiteDieselTrace : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("energy/sites/{code}/diesel-trace", [Authorize] async (
            string code, int? hours, ISender sender, CancellationToken ct) =>
        {
            Result<DieselTraceResponse> result = await sender.Send(new GetSiteDieselTraceQuery(code, hours ?? 24), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Energy);
    }
}
