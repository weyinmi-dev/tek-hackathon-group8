using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Modules.Network.Application.Ingestion.Stage1_Ingest;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Network.Infrastructure.Ingestion.Parsers;

internal sealed class CsvNetworkLogParser : INetworkLogParser
{
    public string Format => "csv";

    public bool CanParse(string contentType, string fileName) =>
        contentType.Contains("csv", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

    public async Task<Result<IReadOnlyList<NetworkEvent>>> ParseAsync(
        Guid ingestionRunId,
        Stream content,
        CancellationToken cancellationToken = default) =>
        await DelimitedRowParser.ParseAsync(ingestionRunId, content, delimiter: ",", cancellationToken);
}

/// <summary>
/// Shared body for CSV and tab-delimited TXT, since they are the same code path
/// with a different separator. Kept internal so the two public parsers stay
/// independently mockable / debuggable.
/// </summary>
internal static class DelimitedRowParser
{
    public static async Task<Result<IReadOnlyList<NetworkEvent>>> ParseAsync(
        Guid ingestionRunId,
        Stream content,
        string delimiter,
        CancellationToken cancellationToken)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            DetectColumnCountChanges = false,
            MissingFieldFound = null,
            BadDataFound = null
        };

        using var reader = new StreamReader(content, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        if (!await csv.ReadAsync().ConfigureAwait(false))
        {
            return Result.Failure<IReadOnlyList<NetworkEvent>>(NetworkLogErrors.EmptyFile());
        }

        csv.ReadHeader();
        string[] headers = csv.HeaderRecord ?? [];

        Result<HeaderIndex> headerResult = HeaderIndex.Build(headers);
        if (headerResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<NetworkEvent>>(headerResult.Error);
        }

        HeaderIndex idx = headerResult.Value;
        var events = new List<NetworkEvent>();
        int rowNumber = 1; // header is row 1; data rows start at 2

        while (await csv.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            Result<NetworkEvent> rowResult = idx.BuildEvent(ingestionRunId, csv, rowNumber);
            if (rowResult.IsFailure)
            {
                return Result.Failure<IReadOnlyList<NetworkEvent>>(rowResult.Error);
            }

            events.Add(rowResult.Value);
        }

        if (events.Count == 0)
        {
            return Result.Failure<IReadOnlyList<NetworkEvent>>(NetworkLogErrors.EmptyFile());
        }

        return Result.Success<IReadOnlyList<NetworkEvent>>(events);
    }
}

/// <summary>
/// Materialised header→column-index lookup, computed once before iterating rows.
/// Avoids a string-comparison loop per cell per row.
/// </summary>
internal sealed class HeaderIndex
{
    private readonly int _timestamp;
    private readonly int _towerCode;
    private readonly int _signalPct;
    private readonly int _loadPct;
    private readonly int _latencyMs;
    private readonly int _status;

    private HeaderIndex(int timestamp, int towerCode, int signalPct, int loadPct, int latencyMs, int status)
    {
        _timestamp = timestamp;
        _towerCode = towerCode;
        _signalPct = signalPct;
        _loadPct = loadPct;
        _latencyMs = latencyMs;
        _status = status;
    }

    public static Result<HeaderIndex> Build(IReadOnlyList<string> headers)
    {
        int Locate(string canonical) =>
            EnumerableIndexOf(headers, h => NetworkLogColumns.MatchesHeader(h, canonical));

        int ts = Locate(NetworkLogColumns.Timestamp);
        int tc = Locate(NetworkLogColumns.TowerCode);

        if (ts < 0)
        {
            return Result.Failure<HeaderIndex>(NetworkLogErrors.MissingColumn(NetworkLogColumns.Timestamp));
        }

        if (tc < 0)
        {
            return Result.Failure<HeaderIndex>(NetworkLogErrors.MissingColumn(NetworkLogColumns.TowerCode));
        }

        return Result.Success(new HeaderIndex(
            ts,
            tc,
            Locate(NetworkLogColumns.SignalPct),
            Locate(NetworkLogColumns.LoadPct),
            Locate(NetworkLogColumns.LatencyMs),
            Locate(NetworkLogColumns.Status)));
    }

    public Result<NetworkEvent> BuildEvent(Guid ingestionRunId, CsvReader csv, int rowNumber)
    {
        string? Read(int index) => index >= 0 ? csv.GetField(index) : null;

        Result<DateTimeOffset> tsResult = NetworkLogColumns.ParseTimestamp(Read(_timestamp), rowNumber);
        if (tsResult.IsFailure)
        {
            return Result.Failure<NetworkEvent>(tsResult.Error);
        }

        Result<string> towerResult = NetworkLogColumns.ParseTowerCode(Read(_towerCode), rowNumber);
        if (towerResult.IsFailure)
        {
            return Result.Failure<NetworkEvent>(towerResult.Error);
        }

        Result<int?> signalResult = NetworkLogColumns.ParseOptionalPercent(Read(_signalPct), NetworkLogColumns.SignalPct, rowNumber);
        if (signalResult.IsFailure)
        {
            return Result.Failure<NetworkEvent>(signalResult.Error);
        }

        Result<int?> loadResult = NetworkLogColumns.ParseOptionalPercent(Read(_loadPct), NetworkLogColumns.LoadPct, rowNumber);
        if (loadResult.IsFailure)
        {
            return Result.Failure<NetworkEvent>(loadResult.Error);
        }

        Result<int?> latencyResult = NetworkLogColumns.ParseOptionalLatency(Read(_latencyMs), rowNumber);
        if (latencyResult.IsFailure)
        {
            return Result.Failure<NetworkEvent>(latencyResult.Error);
        }

        return Result.Success(NetworkEvent.Create(
            ingestionRunId,
            tsResult.Value,
            towerResult.Value,
            signalResult.Value,
            loadResult.Value,
            latencyResult.Value,
            string.IsNullOrWhiteSpace(Read(_status)) ? null : Read(_status),
            rawPayload: null));
    }

    private static int EnumerableIndexOf(IReadOnlyList<string> headers, Func<string, bool> predicate)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            if (predicate(headers[i]))
            {
                return i;
            }
        }
        return -1;
    }
}
