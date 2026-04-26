using Application.Abstractions.Messaging;
using Modules.Alerts.Domain;
using Modules.Alerts.Domain.Alerts;
using SharedKernel;

namespace Modules.Alerts.Application.Alerts.Acknowledge;

internal sealed class AcknowledgeAlertCommandHandler(IAlertRepository alerts, IUnitOfWork uow)
    : ICommandHandler<AcknowledgeAlertCommand>
{
    public async Task<Result> Handle(AcknowledgeAlertCommand request, CancellationToken cancellationToken)
    {
        Alert? alert = await alerts.GetByCodeAsync(request.AlertCode, cancellationToken);
#pragma warning disable IDE0011 // Add braces
        if (alert is null) return Result.Failure(AlertErrors.NotFound);
#pragma warning restore IDE0011 // Add braces

        Result ack = alert.Acknowledge(request.Actor);
#pragma warning disable IDE0011 // Add braces
        if (ack.IsFailure) return ack;
#pragma warning restore IDE0011 // Add braces

        await uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
