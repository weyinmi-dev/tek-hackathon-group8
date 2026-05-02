using SharedKernel;

namespace Modules.Network.Domain.Towers.Events;

public sealed record FuelTheftDetectedDomainEvent(
    Guid TowerId,
    string TowerCode,
    double OldFuelLevel,
    double NewFuelLevel) : IDomainEvent;
