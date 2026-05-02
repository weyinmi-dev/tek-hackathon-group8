using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Modules.Network.Domain;
using Modules.Network.Domain.Towers;
using SharedKernel;

namespace Modules.Network.Application.Towers.RecordFuelReading;

internal sealed class RecordFuelReadingCommandHandler(
    ITowerRepository towerRepository,
    IUnitOfWork unitOfWork,
    ILogger<RecordFuelReadingCommandHandler> logger) : ICommandHandler<RecordFuelReadingCommand>
{
    public async Task<Result> Handle(RecordFuelReadingCommand request, CancellationToken cancellationToken)
    {
        var tower = await towerRepository.GetByCodeAsync(request.TowerCode, cancellationToken);

        if (tower is null)
        {
            return Result.Failure(Error.NotFound("Tower.NotFound", $"Tower with code '{request.TowerCode}' was not found."));
        }

        double oldFuel = tower.FuelLevelLiters;

        // Update the metrics and check for theft
        bool isTheftDetected = tower.UpdatePowerMetrics(request.ActivePowerSource, request.FuelLevelLiters);

        await towerRepository.UpdateAsync(tower, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (isTheftDetected)
        {
            logger.LogWarning("CRITICAL: Fuel theft detected at Tower {TowerCode}. Fuel dropped from {OldFuel} to {NewFuel} instantly.", 
                tower.Code, oldFuel, request.FuelLevelLiters);
            
            // TODO: In the next step, we will trigger an actual Alert via the Alerts Module
        }
        else
        {
            logger.LogInformation("IoT Fuel reading recorded for Tower {TowerCode}: {FuelLevel}L", tower.Code, request.FuelLevelLiters);
        }

        return Result.Success();
    }
}
