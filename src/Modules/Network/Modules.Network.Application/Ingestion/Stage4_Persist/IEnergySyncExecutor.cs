using SharedKernel;

namespace Modules.Network.Application.Ingestion.Stage4_Persist;

/// <summary>
/// Cross-module port: lets Stage 4 synchronise a site's energy plant without Network depending on
/// Energy.Domain. The implementation lives in Energy.Infrastructure, mirroring exactly how
/// <see cref="IAlertActionExecutor"/> is declared here and implemented in Alerts.Infrastructure.
///
/// The executor owns two things Network must not reach into: the Site aggregate (create-or-update,
/// health derivation) and the append-only SiteEnergyLog that every energy trend chart already reads.
/// Writing that log here — rather than inventing a new telemetry store — is what makes snapshot
/// data show up in the existing diesel trace and OPEX views for free.
/// </summary>
public interface IEnergySyncExecutor
{
    Task<Result<EnergySyncResult>> ExecuteAsync(
        IReadOnlyList<EnergySyncRequest> requests,
        CancellationToken cancellationToken = default);
}

public sealed record EnergySyncResult(int SitesCreated, int SitesUpdated, int TelemetryRowsAppended);

/// <summary>
/// Primitive request envelope — all wire types, so the cross-module surface stays free of
/// Energy.Domain enums.
/// </summary>
public sealed record EnergySyncRequest(
    string SiteCode,
    string Name,
    string Region,
    int? BatteryPct,
    int? DieselPct,
    bool GridUp,
    string SourceWire,
    bool HasOpenAlarm,
    string? AnomalyNote,
    DateTime ObservedAtUtc);
