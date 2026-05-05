using System.Text.Json;
using Modules.Network.Application.Ingestion.Stage1_Ingest;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Network.Infrastructure.Ingestion.Parsers;

/// <summary>
/// Parses JSON in either form:
/// <list type="bullet">
///   <item>Top-level array of row objects: <c>[ { "timestamp": "...", "tower_code": "..." }, ... ]</c></item>
///   <item>Envelope: <c>{ "events": [ ... ] }</c> — useful when callers want to attach metadata alongside rows.</item>
/// </list>
/// Field names are matched case-insensitively against <see cref="NetworkLogColumns"/>.
/// </summary>
internal sealed class JsonNetworkLogParser : INetworkLogParser
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
        fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    public async Task<Result<IReadOnlyList<NetworkEvent>>> ParseAsync(
        Guid ingestionRunId,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        JsonDocument doc;
        try
        {
            doc = await JsonDocument.ParseAsync(content, DocumentOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            return Result.Failure<IReadOnlyList<NetworkEvent>>(
                NetworkLogErrors.MalformedFile($"invalid JSON: {ex.Message}"));
        }

        using (doc)
        {
            JsonElement rowsElement = doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => doc.RootElement,
                JsonValueKind.Object when doc.RootElement.TryGetProperty(EnvelopeArrayProperty, out JsonElement envelope) &&
                                          envelope.ValueKind == JsonValueKind.Array => envelope,
                _ => default
            };

            if (rowsElement.ValueKind != JsonValueKind.Array)
            {
                return Result.Failure<IReadOnlyList<NetworkEvent>>(
                    NetworkLogErrors.MalformedFile(
                        "expected a top-level array or an object with an 'events' array property"));
            }

            int arrayLength = rowsElement.GetArrayLength();
            if (arrayLength == 0)
            {
                return Result.Failure<IReadOnlyList<NetworkEvent>>(NetworkLogErrors.EmptyFile());
            }

            var events = new List<NetworkEvent>(arrayLength);
            int rowNumber = 0;

            foreach (JsonElement row in rowsElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowNumber++;

                if (row.ValueKind != JsonValueKind.Object)
                {
                    return Result.Failure<IReadOnlyList<NetworkEvent>>(
                        NetworkLogErrors.MalformedRow(rowNumber, "expected an object"));
                }

                Result<NetworkEvent> rowResult = BuildEvent(ingestionRunId, row, rowNumber);
                if (rowResult.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<NetworkEvent>>(rowResult.Error);
                }

                events.Add(rowResult.Value);
            }

            return Result.Success<IReadOnlyList<NetworkEvent>>(events);
        }
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
