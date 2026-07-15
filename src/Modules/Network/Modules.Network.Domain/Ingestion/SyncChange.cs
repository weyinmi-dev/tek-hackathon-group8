namespace Modules.Network.Domain.Ingestion;

public enum SyncAction
{
    Created = 0,
    Updated = 1,

    /// <summary>Retired, archived, resolved — soft-closed. Nothing in synchronisation hard-deletes.</summary>
    Archived = 2
}

/// <summary>
/// One record an upload actually touched.
///
/// The counts alone ("14 created") tell an operator that something happened but not what, which is
/// exactly the wrong half: when a sync does something surprising, the question is always *which
/// record*. These are the itemised answer, persisted with the run so it is still answerable a week
/// later rather than only in the moment the upload returned.
///
/// Stored as a JSON column on the run, like <see cref="StageTiming"/> — a run's changes are only ever
/// read as a whole, alongside the run, and never queried across runs, so a child table would buy
/// nothing and cost a join.
/// </summary>
public sealed record SyncChange(
    /// <summary>"Tower", "Energy Site", "Equipment", "Maintenance Ticket", "Engineer", "Alert", "Anomaly".</summary>
    string EntityType,

    /// <summary>The record's own identifier — a site code, an equipment id, a ticket id, an alert code.</summary>
    string EntityKey,

    SyncAction Action,

    /// <summary>Site this belongs to, so the table can be grouped when one upload spans several.</summary>
    string? SiteCode,

    /// <summary>Human-readable statement of what changed. Shown verbatim in the report.</summary>
    string? Detail);
