using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Network.Application.Towers.RecordFuelReading;
using Modules.Network.Domain.Towers;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Towers;

public sealed class RecordFuelReadingEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("towers/{code}/fuel", async (
            string code,
            [FromBody] Request request,
            ISender sender,
            CancellationToken cancellationToken) =>
        {
            var command = new RecordFuelReadingCommand(
                TowerCode: code,
                ActivePowerSource: request.ActivePowerSource,
                FuelLevelLiters: request.FuelLevelLiters);

            var result = await sender.Send(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .WithTags("Towers")
        .RequireAuthorization()
        .MapToApiVersion(1)
        .WithName("RecordFuelReading")
        .WithSummary("Record an IoT fuel sensor reading")
        .WithDescription("Updates the live fuel and power metrics for a specified tower and triggers anomaly detection logic.");
    }

    public sealed record Request(PowerSource ActivePowerSource, double FuelLevelLiters);
}
