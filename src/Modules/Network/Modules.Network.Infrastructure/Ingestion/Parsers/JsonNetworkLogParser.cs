using Application.Abstractions.Pipeline;
using System.Text.Json;
using Modules.Network.Application.Ingestion.Stage1_Ingest;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Network.Infrastructure.Ingestion.Parsers;

/// <summary>
/// The single entry point for JSON uploads. Routes on the shape of the root element:
/// <list type="bullet">
///   <item>Top-level array of row objects: <c>[ { "timestamp": "...", "tower_code": "..." }, ... ]</c></item>
///   <item>Envelope: <c>{ "events": [ ... ] }</c> — useful when callers want to attach metadata alongside rows.</item>
///   <item>Site snapshot: <c>{ "site": { ... }, "performanceMetrics": { ... } }</c>, or a batch of them under
///         <c>{ "snapshots": [ ... ] }</c> — handed to <see cref="SiteSnapshotDecoder"/>.</item>
/// </list>
/// Routing on content rather than on file name is deliberate: both feeds arrive as <c>.json</c> with the same
/// content type, so a second registry entry could only be ordered ahead of this one and would swallow the flat
/// logs it does not understand.
/// Field names in the row forms are matched case-insensitively against <see cref="NetworkLogColumns"/>.
/// </summary>
internal sealed class JsonNetworkLogParser(SnapshotCalibrationOptions calibration) : INetworkLogParser
{
    private const string EnvelopeArrayProperty = "events";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public string Format => "json";

    public bool CanParse(string contentType, string fileName) =>
        contentType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);

    public async Task<Result<NetworkLogParseResult>> ParseAsync(
        Guid ingestionRunId,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        JsonDocument doc;
        try
        {
            doc = await JsonDocument.ParseAsync(content, DocumentOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            // Treat malformed JSON as a malformed file rather than attempting a
            // blind JSONL fallback — callers should upload JSONL explicitly.
            if (content.CanSeek) content.Seek(0, SeekOrigin.Begin);
            return Result.Failure<NetworkLogParseResult>(NetworkLogErrors.MalformedFile("invalid JSON"));
        }

        using (doc)
        {
            // A full OSS site snapshot is a document, not a row list: it carries equipment,
            // alarms, maintenance and environment that have no column in a flat log. Route on
            // the root shape so both feeds share one JSON entry point — flat arrays and the
            // { "events": [...] } envelope keep their original path byte for byte.
            if (SiteSnapshotDecoder.IsSnapshotDocument(doc.RootElement))
            {
                return SiteSnapshotDecoder.Decode(ingestionRunId, doc.RootElement, calibration);
            }

            JsonElement rowsElement = doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => doc.RootElement,
                JsonValueKind.Object when doc.RootElement.TryGetProperty(EnvelopeArrayProperty, out JsonElement envelope) &&
                                          envelope.ValueKind == JsonValueKind.Array => envelope,
                _ => default
            };

            if (rowsElement.ValueKind != JsonValueKind.Array)
            {
                return Result.Failure<NetworkLogParseResult>(
                    NetworkLogErrors.MalformedFile(
                        "expected a top-level array, an object with an 'events' array property, " +
                        "or a site snapshot with a 'site' object"));
            }

            int arrayLength = rowsElement.GetArrayLength();
            if (arrayLength == 0)
            {
                return Result.Failure<NetworkLogParseResult>(NetworkLogErrors.EmptyFile());
            }

            var events = new List<NetworkEvent>(arrayLength);
            int rowNumber = 0;

            foreach (JsonElement row in rowsElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowNumber++;

                if (row.ValueKind != JsonValueKind.Object)
                {
                    return Result.Failure<NetworkLogParseResult>(
                        NetworkLogErrors.MalformedRow(rowNumber, "expected an object"));
                }

                Result<NetworkEvent> rowResult = BuildEvent(ingestionRunId, row, rowNumber);
                if (rowResult.IsFailure)
                {
                    return Result.Failure<NetworkLogParseResult>(rowResult.Error);
                }

                events.Add(rowResult.Value);
            }

            return Result.Success(NetworkLogParseResult.FromEvents(events));
        }
    }

    private static async Task<Result<NetworkLogParseResult>> ParseJsonlAsync(
        Guid ingestionRunId,
        Stream content,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, leaveOpen: true);
        var events = new List<NetworkEvent>();
        int rowNumber = 0;
        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            rowNumber++;
            JsonElement element;
            try
            {
                using var rowDoc = JsonDocument.Parse(line, DocumentOptions);
                element = rowDoc.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                return Result.Failure<NetworkLogParseResult>(
                    NetworkLogErrors.MalformedRow(rowNumber, $"invalid JSON: {ex.Message}"));
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                return Result.Failure<NetworkLogParseResult>(
                    NetworkLogErrors.MalformedRow(rowNumber, "expected an object"));
            }

            Result<NetworkEvent> rowResult = BuildEvent(ingestionRunId, element, rowNumber);
            if (rowResult.IsFailure)
            {
                return Result.Failure<NetworkLogParseResult>(rowResult.Error);
            }

            events.Add(rowResult.Value);
        }

        if (events.Count == 0)
        {
            return Result.Failure<NetworkLogParseResult>(NetworkLogErrors.EmptyFile());
        }

        return Result.Success(NetworkLogParseResult.FromEvents(events));
    }

    private static Result<NetworkEvent> BuildEvent(Guid ingestionRunId, JsonElement row, int rowNumber)
    {
        string? rawTimestamp = ReadString(row, NetworkLogColumns.Timestamp);
        string? rawTowerCode = ReadString(row, NetworkLogColumns.TowerCode);
        string? rawSignal = ReadStringOrNumber(row, NetworkLogColumns.SignalPct);
        string? rawLoad = ReadStringOrNumber(row, NetworkLogColumns.LoadPct);
        string? rawLatency = ReadStringOrNumber(row, NetworkLogColumns.LatencyMs);
        string? rawStatus = ReadString(row, NetworkLogColumns.Status);

        Result<DateTimeOffset> tsResult = NetworkLogColumns.ParseTimestamp(rawTimestamp, rowNumber);
        if (tsResult.IsFailure) return Result.Failure<NetworkEvent>(tsResult.Error);

        Result<string> towerResult = NetworkLogColumns.ParseTowerCode(rawTowerCode, rowNumber);
        if (towerResult.IsFailure) return Result.Failure<NetworkEvent>(towerResult.Error);

        Result<int?> signalResult = NetworkLogColumns.ParseOptionalPercent(rawSignal, NetworkLogColumns.SignalPct, rowNumber);
        if (signalResult.IsFailure) return Result.Failure<NetworkEvent>(signalResult.Error);

        Result<int?> loadResult = NetworkLogColumns.ParseOptionalPercent(rawLoad, NetworkLogColumns.LoadPct, rowNumber);
        if (loadResult.IsFailure) return Result.Failure<NetworkEvent>(loadResult.Error);

        Result<int?> latencyResult = NetworkLogColumns.ParseOptionalLatency(rawLatency, rowNumber);
        if (latencyResult.IsFailure) return Result.Failure<NetworkEvent>(latencyResult.Error);

        return Result.Success(NetworkEvent.Create(
            ingestionRunId,
            tsResult.Value,
            towerResult.Value,
            signalResult.Value,
            loadResult.Value,
            latencyResult.Value,
            string.IsNullOrWhiteSpace(rawStatus) ? null : rawStatus,
            rawPayload: row.GetRawText()));
    }

    private static string? ReadString(JsonElement row, string canonical)
    {
        foreach (JsonProperty prop in row.EnumerateObject())
        {
            if (NetworkLogColumns.MatchesHeader(prop.Name, canonical))
            {
                return prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.ToString();
            }
        }
        return null;
    }

    private static string? ReadStringOrNumber(JsonElement row, string canonical)
    {
        foreach (JsonProperty prop in row.EnumerateObject())
        {
            if (!NetworkLogColumns.MatchesHeader(prop.Name, canonical))
            {
                continue;
            }

            return prop.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.Number => prop.Value.GetRawText(),
                JsonValueKind.String => prop.Value.GetString(),
                _ => prop.Value.ToString()
            };
        }
        return null;
    }
}
