using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;

namespace Modules.Network.Application.Ingestion.Stage3_Decide;

/// <summary>
/// Read-only projection of an existing Alert as the decision engine sees it. Keeping
/// this here (instead of accepting the Alerts.Domain entity directly) lets the engine
/// stay a pure function and lets Network.Application avoid a dependency on Alerts.Domain.
/// </summary>
public sealed record AlertSnapshot(
    Guid Id,
    string AnomalyFingerprint,
    PipelineAlertSeverity Severity,
    DateTimeOffset LastSeenAt,
    int OccurrenceCount,
    bool IsResolved);

public sealed record TowerSnapshot(
    string Code,
    string Region,
    string Status,
    int SignalPct,
    int LoadPct);
