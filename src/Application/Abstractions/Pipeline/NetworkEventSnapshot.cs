namespace Application.Abstractions.Pipeline;

/// <summary>
/// A parsed network-log event, flattened for analysis (Phase 3 M12). The Stage-2 analyzer used to
/// take the Network module's <c>NetworkEvent</c> entity directly, which forced the AI side to
/// reference Network. Analysis now runs against this neutral snapshot, so the analyzer contract and
/// its implementation live outside the Network module and neither has to depend on the other.
/// </summary>
public sealed record NetworkEventSnapshot(
    Guid IngestionRunId,
    DateTimeOffset OccurredAt,
    string TowerCode,
    int? SignalPct,
    int? LoadPct,
    int? LatencyMs,
    string? RawStatus);
