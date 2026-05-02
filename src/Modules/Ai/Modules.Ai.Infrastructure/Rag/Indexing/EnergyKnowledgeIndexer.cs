using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag.Indexing;
using Modules.Ai.Application.Rag.Models;
using Modules.Ai.Domain.Knowledge;
using Modules.Energy.Api;

namespace Modules.Ai.Infrastructure.Rag.Indexing;

/// <summary>
/// Converts the Energy module's live state into <see cref="KnowledgeDocumentInput"/> rows
/// and pushes them through <see cref="IRagIndexer"/>. Two document classes are produced:
///
///   • EnergySiteSnapshot — one document per Site, summarising current source mix, battery,
///     diesel, solar, uptime, daily cost, and any open anomaly. Re-indexed each pass so the
///     vector store reflects the latest state. SourceKey: "ENERGY-SITE-{code}".
///
///   • EnergyAnomaly — one document per AnomalyEvent, narrating what was detected, by which
///     model, with what confidence. Append-only — the SourceKey embeds the anomaly id so
///     each detection is its own retrievable chunk.
///
/// Idempotent at the indexer layer: <see cref="IRagIndexer.IndexAsync"/> upserts on SourceKey,
/// so repeated runs replace site snapshots and skip already-seen anomalies.
/// </summary>
public sealed class EnergyKnowledgeIndexer(
    IEnergyApi energy,
    IRagIndexer ragIndexer,
    ILogger<EnergyKnowledgeIndexer> logger)
{
    public async Task<int> IndexAsync(CancellationToken cancellationToken = default)
    {
        var docs = new List<KnowledgeDocumentInput>();

        IReadOnlyList<SiteSnapshot> sites = await energy.ListSitesAsync(cancellationToken);
        foreach (SiteSnapshot s in sites)
        {
            docs.Add(BuildSiteDoc(s));
        }

        IReadOnlyList<AnomalySnapshot> anomalies = await energy.ListAnomaliesAsync(200, cancellationToken);
        foreach (AnomalySnapshot a in anomalies)
        {
            docs.Add(BuildAnomalyDoc(a));
        }

        if (docs.Count == 0)
        {
            return 0;
        }

        IndexResult result = await ragIndexer.IndexBatchAsync(docs, cancellationToken);
        logger.LogInformation(
            "EnergyKnowledgeIndexer: indexed {Docs} documents → {Chunks} chunks (sites={Sites}, anomalies={Anomalies}).",
            result.DocumentsIndexed, result.ChunksIndexed, sites.Count, anomalies.Count);
        return result.DocumentsIndexed;
    }

    private static KnowledgeDocumentInput BuildSiteDoc(SiteSnapshot s)
    {
        StringBuilder body = new();
        body.AppendLine(CultureInfo.InvariantCulture, $"Energy site snapshot — {s.Code} ({s.Name}, {s.Region}).");
        body.AppendLine();
        body.AppendLine(CultureInfo.InvariantCulture, $"Active power source: {s.Source}.");
        body.AppendLine(CultureInfo.InvariantCulture, $"Battery: {s.BattPct}%.  Diesel: {s.DieselPct}%.  Solar output: {s.SolarKw} kW.");
        body.AppendLine(CultureInfo.InvariantCulture, $"Grid: {(s.GridUp ? "up" : "down")}.  Solar installed: {(s.HasSolar ? "yes" : "no")}.");
        body.AppendLine(CultureInfo.InvariantCulture, $"Daily diesel burn: {s.DailyDieselLitres} L.  Daily cost: ₦{s.DailyCostNgn:N0}.");
        body.AppendLine(CultureInfo.InvariantCulture, $"Uptime: {s.UptimePct:N2}%.  Health rating: {s.Health}.");
        if (!string.IsNullOrEmpty(s.AnomalyNote))
        {
            body.AppendLine();
            body.AppendLine(CultureInfo.InvariantCulture, $"Open anomaly note: {s.AnomalyNote}");
        }

        return new KnowledgeDocumentInput(
            SourceKey: $"ENERGY-SITE-{s.Code}",
            Category: KnowledgeCategory.EnergySiteSnapshot,
            Title: $"Energy snapshot — {s.Code} ({s.Name})",
            Region: s.Region,
            Body: body.ToString(),
            Tags: ["energy", "site", s.Code.ToLowerInvariant(), s.Region.ToLowerInvariant().Replace(' ', '-'), s.Source, s.Health],
            OccurredAtUtc: DateTime.UtcNow);
    }

    private static KnowledgeDocumentInput BuildAnomalyDoc(AnomalySnapshot a)
    {
        StringBuilder body = new();
        body.AppendLine(CultureInfo.InvariantCulture, $"Anomaly {a.Id} — site {a.SiteCode}.");
        body.AppendLine();
        body.AppendLine(CultureInfo.InvariantCulture, $"Kind: {a.Kind}.  Severity: {a.Severity}.  Confidence: {Math.Round(a.Confidence * 100)}%.");
        body.AppendLine(CultureInfo.InvariantCulture, $"Detected at: {a.DetectedAtUtc:u}.  Model: {a.Model}.");
        body.AppendLine(CultureInfo.InvariantCulture, $"Status: {(a.Acknowledged ? "acknowledged" : "open")}.");
        body.AppendLine();
        body.AppendLine(CultureInfo.InvariantCulture, $"Detail: {a.Detail}");

        return new KnowledgeDocumentInput(
            SourceKey: $"ENERGY-ANOMALY-{a.Id:N}",
            Category: KnowledgeCategory.EnergyAnomaly,
            Title: $"Energy anomaly — {a.Kind} at {a.SiteCode}",
            Region: "Lagos",
            Body: body.ToString(),
            Tags: ["energy", "anomaly", a.Kind, a.Severity, a.SiteCode.ToLowerInvariant(), a.Model.ToLowerInvariant()],
            OccurredAtUtc: a.DetectedAtUtc);
    }
}
