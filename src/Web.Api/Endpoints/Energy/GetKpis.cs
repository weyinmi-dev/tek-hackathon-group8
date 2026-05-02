using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Energy.Application.Sites.GetEnergyKpis;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Energy;

public sealed class GetKpis : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("energy/kpis", [Authorize] async (ISender sender, CancellationToken ct) =>
        {
            Result<EnergyKpisResponse> result = await sender.Send(new GetEnergyKpisQuery(), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Energy);
    }
}
