using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Alerts.Domain;
using Modules.Alerts.Domain.Alerts;
using Modules.Network.Domain.Towers.Events;

namespace Modules.Alerts.Application.Alerts.FuelTheft;

internal sealed class FuelTheftDetectedDomainEventHandler(
    IAlertRepository alertRepository,
    IUnitOfWork unitOfWork,
    ILogger<FuelTheftDetectedDomainEventHandler> logger) : INotificationHandler<FuelTheftDetectedDomainEvent>
{
    public async Task Handle(FuelTheftDetectedDomainEvent notification, CancellationToken cancellationToken)
    {
        logger.LogWarning("Handling cross-module event: Fuel theft detected at {TowerCode}", notification.TowerCode);

        // Calculate liters lost
        double litersLost = notification.OldFuelLevel - notification.NewFuelLevel;

        var alertCode = $"FT-{notification.TowerCode}-{DateTime.UtcNow:HHmmss}";
        var title = $"CRITICAL: Fuel Theft at {notification.TowerCode}";
        var cause = $"Unnatural fuel drop detected. {litersLost} liters lost instantly while generator was active.";

        // Raise the alert
        var alert = Alert.Raise(
            code: alertCode,
            severity: AlertSeverity.Critical,
            title: title,
            region: "Unknown", // Would ideally fetch from Tower repository, but simplified for event handler
            towerCode: notification.TowerCode,
            aiCause: cause,
            subscribersAffected: 0,
            confidence: 0.99,
            status: AlertStatus.Active,
            raisedAtUtc: DateTime.UtcNow);

        await alertRepository.AddRangeAsync([alert], cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Successfully raised CRITICAL alert {AlertCode} for fuel theft.", alertCode);
    }
}
