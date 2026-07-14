using System.ComponentModel;
using MediatR;
using Modules.Ai.Application.Tools;

namespace Modules.Ai.Agents.Tools;

/// <summary>
/// Synchronisation tools: the Copilot's window onto the state an OSS snapshot upload produced.
///
/// These are tools rather than RAG because the questions they answer are structural, not semantic.
/// "Which alarms are still active?" has an exact answer that changes the moment a snapshot lands;
/// retrieving it from an embedded document would answer from whenever the index was last built and
/// would round-trip precise numbers through prose. The knowledge store stays where it earns its
/// keep — the unstructured corpus — and the live queries answer the live questions.
///
/// Every method is a thin shim over the same MediatR query the HTTP API uses, so the Copilot and the
/// UI can never disagree about what a site's state is.
/// </summary>
public sealed class SiteSyncTools(ISender sender)
{
    [Description(
        "Return the full current state of a site as of its latest synchronised snapshot: health score, " +
        "status, signal, load, active alarms, equipment, environmental readings (temperature, battery, " +
        "generator fuel, grid power), performance metrics, and open maintenance tickets. Use this to " +
        "explain why a site is unhealthy or to list what is currently wrong with it.")]
    public Task<string> GetSiteDetail(
        [Description("Site code, e.g. 'LAG0456'.")] string siteCode,
        CancellationToken cancellationToken = default)
        => ToolResult.DispatchAsync(sender, new GetSiteSyncStateQuery(siteCode), cancellationToken);

    [Description(
        "Return the historical telemetry series for a site over a time range — health score, signal, " +
        "load, latency, temperature, battery, generator fuel, traffic, connected users and KPIs, one " +
        "point per reported snapshot. Use this to describe trends, to say what changed since " +
        "yesterday, or to compare a site against its own past.")]
    public Task<string> GetSiteTelemetry(
        [Description("Site code, e.g. 'LAG0456'.")] string siteCode,
        [Description("How many hours of history to return. 24 for the last day, 168 for the last week.")] int hours,
        CancellationToken cancellationToken = default)
        => ToolResult.DispatchAsync(sender, new GetSiteTelemetryHistoryQuery(siteCode, hours), cancellationToken);

    [Description(
        "Return the synchronisation report for one upload: what was created, updated and archived, how " +
        "long it took, which sites it covered, and any warnings. Use this to summarise an upload.")]
    public Task<string> GetSyncReport(
        [Description("The ingestion run id (a GUID) of the upload.")] string ingestionRunId,
        CancellationToken cancellationToken = default)
        => Guid.TryParse(ingestionRunId, out Guid id)
            ? ToolResult.DispatchAsync(sender, new GetSyncReportQuery(id), cancellationToken)
            : Task.FromResult($"'{ingestionRunId}' is not a valid ingestion run id.");

    [Description(
        "List recent uploads, newest first, with their synchronisation counts and status. Optionally " +
        "narrow to one site. Use this to find an upload before summarising it, or to compare the latest " +
        "upload for a site against the previous one.")]
    public Task<string> ListRecentUploads(
        [Description("Site code to narrow to, e.g. 'LAG0456'. Empty string for all sites.")] string siteCode,
        [Description("How many uploads to return. 10 is a sensible default.")] int take,
        CancellationToken cancellationToken = default)
        => ToolResult.DispatchAsync(
            sender, new ListRecentUploadsQuery(siteCode, take), cancellationToken);
}
