using System.Text.Json;
using Application.Abstractions.Pipeline;
using Modules.Network.Application.Ingestion.Stage1_Ingest;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Network.Infrastructure.Ingestion.Parsers;

/// <summary>
/// Decodes a full OSS site-snapshot document into the canonical
/// <see cref="SiteSnapshotPayload"/> plus the flat <see cref="NetworkEvent"/> reading that
/// the rest of the pipeline scores.
///
/// The flattening is the load-bearing trick: by projecting each snapshot down to one
/// ordinary event row, Stages 2–4 need no knowledge of snapshots at all. The analyzer
/// still sees signal/load/latency and the existing anomaly thresholds still fire, exactly
/// as they do for a CSV upload. The rich half of the document — equipment, alarms,
/// maintenance, environment — travels alongside as the payload and is planned in Stage 3.
///
/// Anything the feed asserts as a *conclusion* rather than a *measurement* is ignored on
/// purpose. A vendor's own risk score or recommended action would, if honoured, silently
/// outrank our thresholds and make the upstream system the author of our alerts.
/// </summary>
internal static class SiteSnapshotDecoder
{
    /// <summary>Root property that marks a JSON document as a site snapshot rather than a flat log.</summary>
    public const string SiteProperty = "site";

    /// <summary>Envelope property carrying many snapshots in one file — a batched OSS feed.</summary>
    public const string BatchProperty = "snapshots";

    /// <summary>
    /// True when the root element is a snapshot document (single or batched). Used by the JSON
    /// parser to route; a false answer means the document is a flat event log and takes the
    /// original code path untouched.
    /// </summary>
    public static bool IsSnapshotDocument(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (root.TryGetProperty(SiteProperty, out JsonElement site) && site.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        return root.TryGetProperty(BatchProperty, out JsonElement batch) && batch.ValueKind == JsonValueKind.Array;
    }

    public static Result<NetworkLogParseResult> Decode(Guid ingestionRunId, JsonElement root)
    {
        List<JsonElement> documents = [];

        if (root.TryGetProperty(BatchProperty, out JsonElement batch) && batch.ValueKind == JsonValueKind.Array)
        {
            documents.AddRange(batch.EnumerateArray());
            if (documents.Count == 0)
            {
                return Result.Failure<NetworkLogParseResult>(NetworkLogErrors.EmptyFile());
            }
        }
        else
        {
            documents.Add(root);
        }

        var payloads = new List<SiteSnapshotPayload>(documents.Count);
        var events = new List<NetworkEvent>(documents.Count);

        int index = 0;
        foreach (JsonElement document in documents)
        {
            index++;

            Result<SiteSnapshotPayload> decoded = DecodeOne(document, index);
            if (decoded.IsFailure)
            {
                return Result.Failure<NetworkLogParseResult>(decoded.Error);
            }

            SiteSnapshotPayload payload = decoded.Value;
            payloads.Add(payload);
            events.Add(ToNetworkEvent(ingestionRunId, payload, document));
        }

        return Result.Success(new NetworkLogParseResult(events, payloads));
    }

    private static Result<SiteSnapshotPayload> DecodeOne(JsonElement document, int index)
    {
        if (document.ValueKind != JsonValueKind.Object)
        {
            return Result.Failure<SiteSnapshotPayload>(
                NetworkLogErrors.MalformedRow(index, "expected a snapshot object"));
        }

        SiteSnapshotPayload? payload;
        try
        {
            payload = document.Deserialize<SiteSnapshotPayload>(SiteSnapshotPayload.SerializerOptions);
        }
        catch (JsonException ex)
        {
            return Result.Failure<SiteSnapshotPayload>(
                NetworkLogErrors.MalformedRow(index, $"invalid site snapshot: {ex.Message}"));
        }

        if (payload?.Site is null)
        {
            return Result.Failure<SiteSnapshotPayload>(
                NetworkLogErrors.MalformedRow(index, "snapshot is missing the required 'site' object"));
        }

        if (string.IsNullOrWhiteSpace(payload.Site.SiteCode))
        {
            return Result.Failure<SiteSnapshotPayload>(
                NetworkLogErrors.MalformedRow(index, "snapshot is missing the required 'site.siteCode'"));
        }

        // Normalise the collections so every downstream consumer can enumerate without a null
        // check, and pin the site code to the same upper-invariant form NetworkEvent.Create and
        // Tower.Code use — it is the join key across all three modules.
        return Result.Success(payload with
        {
            Site = payload.Site with
            {
                SiteCode = payload.Site.SiteCode.Trim().ToUpperInvariant(),
                Technology = payload.Site.Technology ?? [],
                Equipment = payload.Site.Equipment ?? []
            },
            ActiveAlarms = payload.ActiveAlarms ?? [],
            Performance = payload.Performance is null
                ? null
                : payload.Performance with { Kpis = payload.Performance.Kpis ?? [] },
            Maintenance = payload.Maintenance is null
                ? null
                : payload.Maintenance with
                {
                    OpenTickets = payload.Maintenance.OpenTickets ?? [],
                    MaintenanceHistory = payload.Maintenance.MaintenanceHistory ?? []
                }
        });
    }

    /// <summary>
    /// Projects a snapshot onto the flat reading the analyzer consumes. The whole document is
    /// kept verbatim in <c>RawPayload</c>, so nothing is lost by the flattening — Stage 3 reads
    /// the full picture back from the stored snapshot, not from this row.
    /// </summary>
    private static NetworkEvent ToNetworkEvent(Guid ingestionRunId, SiteSnapshotPayload payload, JsonElement document)
    {
        SnapshotPerformanceMetrics? performance = payload.Performance;

        // The reading is stamped with the moment the measurements were taken, not the moment the
        // provider serialised the document. Falling back to GeneratedAt keeps a snapshot with no
        // performance block on the timeline rather than dropping it.
        DateTimeOffset occurredAt = performance?.CapturedAt ?? payload.GeneratedAt;

        return NetworkEvent.Create(
            ingestionRunId,
            occurredAt,
            payload.Site.SiteCode,
            signalPct: SnapshotDerivations.SignalPctFromRsrp(
                SnapshotDerivations.Kpi(performance?.Kpis, "RSRP")),
            loadPct: performance?.CellUtilizationPercent,
            latencyMs: performance?.LatencyMs,
            rawStatus: SnapshotDerivations.TowerStatusFrom(payload.Site.HealthScore, payload.ActiveAlarms),
            rawPayload: document.GetRawText());
    }
}
