namespace Application.Abstractions.Pipeline;

public sealed record TopologyDelta(
    IReadOnlyList<TowerStatusChange> StatusChanges,
    IReadOnlyList<TowerMetricUpdate> MetricUpdates);

public sealed record TowerStatusChange(
    string TowerCode,
    string PreviousStatus,
    string NewStatus,
    string? Reason);

public sealed record TowerMetricUpdate(
    string TowerCode,
    int? SignalPct,
    int? LoadPct,
    int? LatencyMs);
