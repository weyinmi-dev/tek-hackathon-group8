using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Application.Abstractions.Caching;
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
    ICacheService cache,
    ILogger<EnergyKnowledgeIndexer> logger)
{
    private static readonly TimeSpan HashTtl = TimeSpan.FromDays(7);

    public async Task<int> IndexAsync(CancellationToken cancellationToken = default)
    {
        var candidates = new List<KnowledgeDocumentInput>();

        IReadOnlyList<SiteSnapshot> sites = await energy.ListSitesAsync(cancellationToken);
        foreach (SiteSnapshot s in sites)
        {
            candidates.Add(BuildSiteDoc(s));
        }

        IReadOnlyList<AnomalySnapshot> anomalies = await energy.ListAnomaliesAsync(200, cancellationToken);
        foreach (AnomalySnapshot a in anomalies)
        {
            candidates.Add(BuildAnomalyDoc(a));
        }

        if (candidates.Count == 0)
        {
            return 0;
        }

        // Dirty check (Phase 3 M14). This runs every five minutes over a fleet whose text is usually
        // byte-for-byte identical to the last pass; without it the indexer rewrites and re-embeds the
        // whole corpus on every tick. Only documents whose content hash actually moved are re-indexed.
        var docs = new List<KnowledgeDocumentInput>();
        var pendingHashes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (KnowledgeDocumentInput doc in candidates)
        {
            string key = HashKey(doc.SourceKey);
            string hash = ContentHash(doc);

            string? lastSeen = await cache.GetAsync<string>(key, cancellationToken);
            if (string.Equals(lastSeen, hash, StringComparison.Ordinal))
            {
                continue;
            }

            docs.Add(doc);
            pendingHashes[key] = hash;
        }

        if (docs.Count == 0)
        {
            logger.LogDebug(
                "EnergyKnowledgeIndexer: no content changed ({Sites} sites, {Anomalies} anomalies) — nothing re-indexed.",
                sites.Count, anomalies.Count);
            return 0;
        }

        IndexResult result = await ragIndexer.IndexBatchAsync(docs, cancellationToken);

        // Record hashes only after a successful index, so a failed pass is retried next tick rather
        // than being silently marked clean.
        foreach ((string key, string hash) in pendingHashes)
        {
            await cache.SetAsync(key, hash, HashTtl, cancellationToken);
        }

        logger.LogInformation(
            "EnergyKnowledgeIndexer: indexed {Docs} changed documents → {Chunks} chunks; skipped {Skipped} unchanged (sites={Sites}, anomalies={Anomalies}).",
            result.DocumentsIndexed, result.ChunksIndexed, candidates.Count - docs.Count, sites.Count, anomalies.Count);
        return result.DocumentsIndexed;
    }

    private static string HashKey(string sourceKey) => $"energy-index:{sourceKey}";

    private static string ContentHash(KnowledgeDocumentInput doc) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{doc.Title}\n{doc.Region}\n{doc.Body}")));

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
