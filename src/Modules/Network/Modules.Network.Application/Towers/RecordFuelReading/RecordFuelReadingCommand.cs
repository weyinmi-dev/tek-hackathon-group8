using Application.Abstractions.Messaging;
using Modules.Network.Domain.Towers;

namespace Modules.Network.Application.Towers.RecordFuelReading;

public sealed record RecordFuelReadingCommand(
    string TowerCode,
    PowerSource ActivePowerSource,
    double FuelLevelLiters) : ICommand;
