using Application.Abstractions.Messaging;
using Modules.Alerts.Domain;
using Modules.Alerts.Domain.Alerts;
using Modules.Analytics.Api;
using SharedKernel;

namespace Modules.Alerts.Application.Alerts.Assign;

public sealed record AssignAlertCommand(string AlertCode, string Team, string ActorHandle, string ActorRole) : ICommand;

internal sealed class AssignAlertCommandHandler(
    IAlertRepository alerts,
    IUnitOfWork uow,
    IAnalyticsApi analytics)
    : ICommandHandler<AssignAlertCommand>
{
    public async Task<Result> Handle(AssignAlertCommand request, CancellationToken cancellationToken)
    {
        Alert? alert = await alerts.GetByCodeAsync(request.AlertCode, cancellationToken);
        if (alert is null)
        {
            return Result.Failure(AlertErrors.NotFound);
        }

        Result assignment = alert.AssignToTeam(request.Team, request.ActorHandle);
        if (assignment.IsFailure)
        {
            return assignment;
        }

        await uow.SaveChangesAsync(cancellationToken);

        await analytics.RecordAsync(
            actor: request.ActorHandle,
            role: request.ActorRole,
            action: "alert.assign",
            target: $"{alert.Code} → {request.Team}",
            sourceIp: "-",
            cancellationToken);

        return Result.Success();
    }
}
