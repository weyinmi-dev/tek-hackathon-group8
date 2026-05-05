using Modules.Network.Domain.Ingestion;

namespace Modules.Network.Application.Ingestion.Stage3_Decide;

/// <summary>
/// Thresholds the rule-based decision engine uses to filter and route AI output.
/// Defaults are deliberately conservative — meant to be tuned from configuration
/// once we have field-data on false-positive rates.
/// </summary>
public sealed record DecisionEngineOptions
{
    /// <summary>
    /// Anomalies below this confidence are dropped without producing any action.
    /// AI noise floor; escalating below ~0.5 historically produces too many spurious alerts.
    /// </summary>
    public decimal MinAnomalyConfidence { get; init; } = 0.5m;

    /// <summary>
    /// Optimizations whose <c>EstimatedImpact</c> is below this value are dropped.
    /// </summary>
    public decimal MinOptimizationImpact { get; init; } = 0.2m;

    /// <summary>
    /// Per-percent metric jitter we treat as noise. A signal/load update only produces
    /// an UpdateTowerAction if the new value differs from the current snapshot by more
    /// than this many percentage points.
    /// </summary>
    public int MetricDeltaPercentThreshold { get; init; } = 5;

    /// <summary>
    /// Latency is in ms, not %, so it gets its own absolute threshold.
    /// </summary>
    public int LatencyDeltaMsThreshold { get; init; } = 10;

    /// <summary>
    /// Time bucket used by <see cref="Fingerprints.AnomalyFingerprint"/> to dedupe
    /// anomalies. Falls back to the domain default when null.
    /// </summary>
    public TimeSpan? AnomalyTimeBucket { get; init; }

    public TimeSpan EffectiveAnomalyTimeBucket =>
        AnomalyTimeBucket ?? Fingerprints.DefaultAnomalyTimeBucket;
}
