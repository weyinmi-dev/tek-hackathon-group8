namespace Application.Abstractions.Pipeline;

/// <summary>
/// The closed set of anomaly categories the AI is allowed to emit. Constraining
/// the enum here (instead of accepting free-form strings) is what makes the
/// AI output schema-validatable: anything outside this set is rejected before
/// reaching the decision layer.
/// </summary>
public enum AnomalyType
{
    SignalDrop = 0,
    LoadSpike = 1,
    OutagePattern = 2,
    LatencyAnomaly = 3,
    PacketLoss = 4,
    PowerInstability = 5
}

public enum OptimizationType
{
    LoadBalance = 0,
    PowerAdjust = 1,
    RouteReconfigure = 2,
    AntennaRetune = 3,
    CapacityExpansion = 4
}

/// <summary>
/// Pipeline-local severity. Mirrors the Alerts module's enum but stays decoupled
/// from it so Network.Application doesn't take a dependency on Alerts.Domain.
/// The Alerts module maps this at its command boundary.
/// </summary>
public enum PipelineAlertSeverity
{
    Info = 0,
    Warn = 1,
    Critical = 2
}
