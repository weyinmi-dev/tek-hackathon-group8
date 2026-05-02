using Application.Abstractions.Messaging;
using Modules.Alerts.Domain;
using Modules.Alerts.Domain.Alerts;
using Modules.Analytics.Api;
using SharedKernel;

namespace Modules.Alerts.Application.Alerts.Dispatch;

public sealed record DispatchAlertCommand(string AlertCode, string Target, string ActorHandle, string ActorRole) : ICommand;

internal sealed class DispatchAlertCommandHandler(
    IAlertRepository alerts,
    IUnitOfWork uow,
    IAnalyticsApi analytics)
    : ICommandHandler<DispatchAlertCommand>
{
    public async Task<Result> Handle(DispatchAlertCommand request, CancellationToken cancellationToken)
    {
        Alert? alert = await alerts.GetByCodeAsync(request.AlertCode, cancellationToken);
        if (alert is null)
        {
            return Result.Failure(AlertErrors.NotFound);
        }

        Result dispatch = alert.DispatchField(request.Target, request.ActorHandle);
        if (dispatch.IsFailure)
        {
            return dispatch;
        }

        await uow.SaveChangesAsync(cancellationToken);

        await analytics.RecordAsync(
            actor: request.ActorHandle,
            role: request.ActorRole,
            action: "alert.dispatch",
            target: $"{alert.Code} → {request.Target}",
            sourceIp: "-",
            cancellationToken);

        return Result.Success();
    }
}
