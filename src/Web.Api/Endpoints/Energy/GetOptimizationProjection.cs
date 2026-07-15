using MediatR;
using Microsoft.AspNetCore.Authorization;
using Modules.Energy.Application.Optimization.GetOptimization;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Energy;

public sealed class GetOptimizationProjection : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        // GET /api/energy/optimize/projection?solar=44&diesel=900&batt=70 — pure compute over
        // the live fleet, used by the Optimization page sliders.
        app.MapGet("energy/optimize/projection", [Authorize] async (
            int? solar, int? diesel, int? batt, ISender sender, CancellationToken ct) =>
        {
            int s = Math.Clamp(solar ?? 44, 0, 100);

            // Diesel ceiling matches the slider's maximum (₦3000/L). The two must move together:
            // a clamp below the slider's range would let an operator drag past it and watch the
            // projection stop responding, with nothing to say why.
            int d = Math.Clamp(diesel ?? 900, 700, 3000);

            int b = Math.Clamp(batt ?? 70, 30, 95);

            Result<OptimizationProjectionResponse> result = await sender.Send(
                new GetOptimizationProjectionQuery(s, d, b), ct);
            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .WithTags(Tags.Energy);
    }
}
