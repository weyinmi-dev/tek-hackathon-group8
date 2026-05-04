using Application.Abstractions.Messaging;
using Modules.Analytics.Api;
using Modules.Energy.Domain;
using Modules.Energy.Domain.Events;
using Modules.Energy.Domain.Sites;
using SharedKernel;

namespace Modules.Energy.Application.Anomalies.AcknowledgeAnomaly;

public sealed record AcknowledgeAnomalyCommand(Guid AnomalyId, string ActorHandle, string ActorRole)
    : ICommand;

internal sealed class AcknowledgeAnomalyCommandHandler(
    IAnomalyEventRepository anomalies,
    ISiteRepository sites,
    IUnitOfWork uow,
    IAnalyticsApi analytics)
    : ICommandHandler<AcknowledgeAnomalyCommand>
{
    public async Task<Result> Handle(AcknowledgeAnomalyCommand request, CancellationToken cancellationToken)
    {
        AnomalyEvent? ev = await anomalies.GetAsync(request.AnomalyId, cancellationToken);
        if (ev is null)
        {
            return Result.Failure(Error.Problem("Energy.AnomalyNotFound", $"Anomaly {request.AnomalyId} not found."));
        }

        ev.Acknowledge(request.ActorHandle);

        // Clear the open-anomaly flag on the site if no other open anomalies remain.
        IReadOnlyList<AnomalyEvent> stillOpen = await anomalies.ListOpenForSiteAsync(ev.SiteCode, cancellationToken);
        if (stillOpen.Count == 0)
        {
            Site? site = await sites.GetByCodeAsync(ev.SiteCode, cancellationToken);
            site?.SetAnomalyNote(null);
        }

        await uow.SaveChangesAsync(cancellationToken);

        await analytics.RecordAsync(
            actor: request.ActorHandle,
            role: request.ActorRole,
            action: "energy.anomaly_ack",
            target: $"{ev.SiteCode} · {ev.Kind.ToWire()} · {ev.Id}",
            sourceIp: "-",
            cancellationToken);

        return Result.Success();
    }
}
