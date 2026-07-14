using Application.Abstractions.Pipeline;
using Modules.Network.Domain.Ingestion;

namespace Modules.Network.Application.Ingestion.Stage1_Ingest;

/// <summary>
/// What Stage 1 produces from one uploaded file.
///
/// <see cref="Events"/> is the flat, per-reading time series every downstream stage already
/// understands — it is what the analyzer scores and the decision engine turns into alerts.
/// Every parser produces it, and for the row-oriented formats (CSV, TXT, XLSX, flat JSON)
/// it is the whole story.
///
/// <see cref="Snapshots"/> is the richer document a full OSS site snapshot also carries:
/// equipment, alarms, maintenance, environmental readings — facts that have no column in a
/// flat log row. Only the snapshot parser fills it. Keeping both on one return type means
/// there is still exactly one parser contract and one Stage-1 code path; a snapshot upload
/// is a *shape* of input, not a second pipeline.
/// </summary>
public sealed record NetworkLogParseResult(
    IReadOnlyList<NetworkEvent> Events,
    IReadOnlyList<SiteSnapshotPayload> Snapshots)
{
    /// <summary>A row-oriented parse: readings only, no snapshot document.</summary>
    public static NetworkLogParseResult FromEvents(IReadOnlyList<NetworkEvent> events) => new(events, []);
}
