using SharedKernel;

namespace Modules.Network.Application.Ingestion.Stage4_Persist;

/// <summary>
/// Cross-module port: lets Network's Stage-4 handler hand off enriched alert actions to
/// the Alerts module without depending on Alerts.Domain or Alerts.Application. The
/// implementation lives in Alerts.Infrastructure (alongside the matching
/// <c>IAlertSnapshotProvider</c> impl) and dispatches one MediatR command per request
/// so each alert mutation rides the standard logging + validation pipeline.
/// </summary>
public interface IAlertActionExecutor
{
    Task<Result<AlertActionsResult>> ExecuteAsync(
        IReadOnlyList<AlertActionRequest> requests,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes alerts whose upstream alarm has cleared — it was live here but is absent from the
    /// provider's latest snapshot of that site.
    ///
    /// Only ever called with fingerprints the planner has already established are OSS-sourced and
    /// belong to a site covered by this upload. Returns the number actually resolved; alerts already
    /// resolved are skipped, so a repeated snapshot that keeps omitting an alarm is a no-op.
    /// </summary>
    Task<Result<int>> ResolveAsync(
        IReadOnlyList<AlertResolutionRequest> resolutions,
        CancellationToken cancellationToken = default);
}

public sealed record AlertResolutionRequest(string AnomalyFingerprint, string Reason);

public sealed record AlertActionsResult(int AlertsCreated, int AlertsUpdated);

/// <summary>
/// Primitive request envelope. Network resolves region/title/severity from the
/// PipelineAction + tower snapshots before calling the executor — all string-typed
/// to keep the cross-module surface free of Alerts.Domain types.
/// </summary>
public sealed record AlertActionRequest(
    string AnomalyFingerprint,
    Guid? ExistingAlertId,
    string SeverityWire,
    string TowerCode,
    string Region,
    string Title,
    string AiCause,
    double Confidence,
    int SubscribersAffected,
    DateTime DetectedAtUtc);
