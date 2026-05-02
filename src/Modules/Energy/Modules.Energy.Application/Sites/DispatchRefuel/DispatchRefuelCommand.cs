using Application.Abstractions.Messaging;
using Modules.Analytics.Api;
using Modules.Energy.Domain;
using Modules.Energy.Domain.Events;
using Modules.Energy.Domain.Sites;
using SharedKernel;

namespace Modules.Energy.Application.Sites.DispatchRefuel;

public sealed record DispatchRefuelCommand(string SiteCode, int LitresAdded, string ActorHandle, string ActorRole)
    : ICommand<DispatchRefuelResponse>;

public sealed record DispatchRefuelResponse(string SiteCode, int DieselPctAfter, int PctChange);

internal sealed class DispatchRefuelCommandHandler(
    ISiteRepository sites,
    IFuelEventRepository fuelEvents,
    IUnitOfWork uow,
    IAnalyticsApi analytics)
    : ICommandHandler<DispatchRefuelCommand, DispatchRefuelResponse>
{
    public async Task<Result<DispatchRefuelResponse>> Handle(DispatchRefuelCommand request, CancellationToken cancellationToken)
    {
        if (request.LitresAdded is <= 0 or > 200)
        {
            return Result.Failure<DispatchRefuelResponse>(Error.Problem("Energy.InvalidRefuel", "Refuel must be 1-200 litres."));
        }

        Site? site = await sites.GetByCodeAsync(request.SiteCode, cancellationToken);
        if (site is null)
        {
            return Result.Failure<DispatchRefuelResponse>(Error.Problem("Energy.SiteNotFound", $"Site {request.SiteCode} not found."));
        }

        int change = site.RecordRefuel(request.LitresAdded);

        await fuelEvents.AddAsync(
            FuelEvent.Record(site.Code, FuelEventKind.Refuel, request.LitresAdded, site.DieselPct,
                $"Refuel dispatched by {request.ActorHandle}"),
            cancellationToken);

        await uow.SaveChangesAsync(cancellationToken);

        await analytics.RecordAsync(
            actor: request.ActorHandle,
            role: request.ActorRole,
            action: "energy.dispatch_refuel",
            target: $"{site.Code} +{request.LitresAdded}L",
            sourceIp: "-",
            cancellationToken);

        return Result.Success(new DispatchRefuelResponse(site.Code, site.DieselPct, change));
    }
}
