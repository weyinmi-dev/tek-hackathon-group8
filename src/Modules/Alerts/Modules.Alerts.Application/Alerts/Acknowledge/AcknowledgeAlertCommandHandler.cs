using Application.Abstractions.Messaging;
using Modules.Alerts.Domain.Alerts;
using SharedKernel;

namespace Modules.Alerts.Application.Alerts.Acknowledge;

internal sealed class AcknowledgeAlertCommandHandler(IAlertRepository alerts)
    : ICommandHandler<AcknowledgeAlertCommand>
{
    public async Task<Result> Handle(AcknowledgeAlertCommand request, CancellationToken cancellationToken)
    {
        Alert? alert = await alerts.GetByCodeAsync(request.AlertCode, cancellationToken);
        if (alert is null) return Result.Failure(AlertErrors.NotFound);

        Result ack = alert.Acknowledge(request.Actor);
        if (ack.IsFailure) return ack;

        await alerts.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
