using Modules.Alerts.Domain.Alerts;
using Application.Abstractions.Pipeline;
using Modules.Network.Application.Ingestion.Stage3_Decide;

namespace Modules.Alerts.Infrastructure.Pipeline;

/// <summary>
/// Implements the consumer-driven contract Network defined in PR 3. Returns every
/// fingerprinted alert that's still in a non-resolved state — the decision engine
/// uses this to choose CREATE vs UPDATE for new anomalies.
/// </summary>
internal sealed class AlertSnapshotProvider(IAlertRepository alerts) : IAlertSnapshotProvider
{
    public async Task<IReadOnlyList<AlertSnapshot>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Alert> active = await alerts.ListActiveFingerprintedAsync(cancellationToken);

        var snapshots = new List<AlertSnapshot>(active.Count);
        foreach (Alert alert in active)
        {
            // Filter is `AnomalyFingerprint != null` — null check is just for the
            // analyzer's null-flow.
            if (alert.AnomalyFingerprint is null)
            {
                continue;
            }

            snapshots.Add(new AlertSnapshot(
                Id: alert.Id,
                AnomalyFingerprint: alert.AnomalyFingerprint,
                Severity: ToWire(alert.Severity),
                LastSeenAt: alert.LastSeenAtUtc is { } seen
                    ? new DateTimeOffset(DateTime.SpecifyKind(seen, DateTimeKind.Utc))
                    : new DateTimeOffset(DateTime.SpecifyKind(alert.RaisedAtUtc, DateTimeKind.Utc)),
                OccurrenceCount: alert.OccurrenceCount ?? 1,
                IsResolved: false));
        }

        return snapshots;
    }

    private static PipelineAlertSeverity ToWire(AlertSeverity s) => s switch
    {
        AlertSeverity.Critical => PipelineAlertSeverity.Critical,
        AlertSeverity.Warn => PipelineAlertSeverity.Warn,
        _ => PipelineAlertSeverity.Info
    };
}
