using Application.Abstractions.Messaging;
using Modules.Analytics.Api;
using Modules.Energy.Domain;
using Modules.Energy.Domain.Sites;
using SharedKernel;

namespace Modules.Energy.Application.Sites.SwitchSource;

/// <summary>
/// Engineer-visible action: change the active power source for a site. Audited via Analytics
/// so it shows up in the Audit Log alongside the rest of the operator actions.
/// </summary>
public sealed record SwitchSiteSourceCommand(string SiteCode, string Source, string ActorHandle, string ActorRole)
    : ICommand<SwitchSiteSourceResponse>;

public sealed record SwitchSiteSourceResponse(string SiteCode, string Source, string Health);

internal sealed class SwitchSiteSourceCommandHandler(
    ISiteRepository sites,
    IUnitOfWork uow,
    IAnalyticsApi analytics)
    : ICommandHandler<SwitchSiteSourceCommand, SwitchSiteSourceResponse>
{
    public async Task<Result<SwitchSiteSourceResponse>> Handle(SwitchSiteSourceCommand request, CancellationToken cancellationToken)
    {
        Site? site = await sites.GetByCodeAsync(request.SiteCode, cancellationToken);
        if (site is null)
        {
            return Result.Failure<SwitchSiteSourceResponse>(Error.Problem("Energy.SiteNotFound", $"Site {request.SiteCode} not found."));
        }

        PowerSource newSource = PowerSourceExtensions.FromWire(request.Source);
        site.SwitchSource(newSource);
        await uow.SaveChangesAsync(cancellationToken);

        await analytics.RecordAsync(
            actor: request.ActorHandle,
            role: request.ActorRole,
            action: "energy.switch_source",
            target: $"{site.Code} → {newSource.ToWire()}",
            sourceIp: "-",
            cancellationToken);

        return Result.Success(new SwitchSiteSourceResponse(site.Code, site.Source.ToWire(), site.Health.ToWire()));
    }
}
