using ClosedXML.Excel;
using Modules.Network.Application.Ingestion.Stage1_Ingest;
using Modules.Network.Domain.Ingestion;
using SharedKernel;

namespace Modules.Network.Infrastructure.Ingestion.Parsers;

/// <summary>
/// Reads the first worksheet, treats row 1 as headers, and translates each subsequent
/// non-empty row into a <see cref="NetworkEvent"/>. ClosedXML loads the workbook into
/// memory; large XLSX uploads should be size-capped at the API boundary.
/// </summary>
internal sealed class XlsxNetworkLogParser : INetworkLogParser
{
    public string Format => "xlsx";

    public bool CanParse(string contentType, string fileName) =>
        contentType.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase) ||
        contentType.Contains("excel", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);

    public Task<Result<NetworkLogParseResult>> ParseAsync(
        Guid ingestionRunId,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        XLWorkbook? workbook = null;
        try
        {
            try
            {
                workbook = new XLWorkbook(content);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Task.FromResult(Result.Failure<NetworkLogParseResult>(
                    NetworkLogErrors.MalformedFile($"invalid XLSX: {ex.Message}")));
            }

            IXLWorksheet? sheet = workbook.Worksheets.FirstOrDefault();
            if (sheet is null)
            {
                return Task.FromResult(Result.Failure<NetworkLogParseResult>(NetworkLogErrors.EmptyFile()));
            }

            IXLRow? headerRow = sheet.FirstRowUsed();
            if (headerRow is null)
            {
                return Task.FromResult(Result.Failure<NetworkLogParseResult>(NetworkLogErrors.EmptyFile()));
            }

            string[] headers = headerRow.CellsUsed().Select(c => c.GetString()).ToArray();

            Result<XlsxHeaderIndex> headerResult = XlsxHeaderIndex.Build(headers);
            if (headerResult.IsFailure)
            {
                return Task.FromResult(Result.Failure<NetworkLogParseResult>(headerResult.Error));
            }

            XlsxHeaderIndex idx = headerResult.Value;
            var events = new List<NetworkEvent>();

            foreach (IXLRow row in sheet.RowsUsed().Skip(1))
            {
                cancellationToken.ThrowIfCancellationRequested();

                int rowNumber = row.RowNumber();

                if (row.IsEmpty())
                {
                    continue;
                }

                Result<NetworkEvent> rowResult = idx.BuildEvent(ingestionRunId, row, rowNumber);
                if (rowResult.IsFailure)
                {
                    return Task.FromResult(Result.Failure<NetworkLogParseResult>(rowResult.Error));
                }

                events.Add(rowResult.Value);
            }

            if (events.Count == 0)
            {
                return Task.FromResult(Result.Failure<NetworkLogParseResult>(NetworkLogErrors.EmptyFile()));
            }

            return Task.FromResult(Result.Success(NetworkLogParseResult.FromEvents(events)));
        }
        finally
        {
            workbook?.Dispose();
        }
    }
}

internal sealed class XlsxHeaderIndex
{
    private readonly int _timestamp;
    private readonly int _towerCode;
    private readonly int _signalPct;
    private readonly int _loadPct;
    private readonly int _latencyMs;
    private readonly int _status;

    private XlsxHeaderIndex(int timestamp, int towerCode, int signalPct, int loadPct, int latencyMs, int status)
    {
        _timestamp = timestamp;
        _towerCode = towerCode;
        _signalPct = signalPct;
        _loadPct = loadPct;
        _latencyMs = latencyMs;
        _status = status;
    }

    public static Result<XlsxHeaderIndex> Build(IReadOnlyList<string> headers)
    {
        // ClosedXML cell column numbers are 1-based; header[0] sits in column 1.
        int Locate(string canonical)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                if (NetworkLogColumns.MatchesHeader(headers[i], canonical))
                {
                    return i + 1;
                }
            }
            return -1;
        }

        int ts = Locate(NetworkLogColumns.Timestamp);
        int tc = Locate(NetworkLogColumns.TowerCode);

        if (ts < 0)
        {
            return Result.Failure<XlsxHeaderIndex>(NetworkLogErrors.MissingColumn(NetworkLogColumns.Timestamp));
        }

        if (tc < 0)
        {
            return Result.Failure<XlsxHeaderIndex>(NetworkLogErrors.MissingColumn(NetworkLogColumns.TowerCode));
        }

        return Result.Success(new XlsxHeaderIndex(
            ts,
            tc,
            Locate(NetworkLogColumns.SignalPct),
            Locate(NetworkLogColumns.LoadPct),
            Locate(NetworkLogColumns.LatencyMs),
            Locate(NetworkLogColumns.Status)));
    }

    public Result<NetworkEvent> BuildEvent(Guid ingestionRunId, IXLRow row, int rowNumber)
    {
        string? Read(int column) => column > 0 ? CellAsString(row.Cell(column)) : null;

        Result<DateTimeOffset> tsResult = ParseTimestampFromCell(row.Cell(_timestamp), rowNumber);
        if (tsResult.IsFailure) return Result.Failure<NetworkEvent>(tsResult.Error);

        Result<string> towerResult = NetworkLogColumns.ParseTowerCode(Read(_towerCode), rowNumber);
        if (towerResult.IsFailure) return Result.Failure<NetworkEvent>(towerResult.Error);

        Result<int?> signalResult = NetworkLogColumns.ParseOptionalPercent(Read(_signalPct), NetworkLogColumns.SignalPct, rowNumber);
        if (signalResult.IsFailure) return Result.Failure<NetworkEvent>(signalResult.Error);

        Result<int?> loadResult = NetworkLogColumns.ParseOptionalPercent(Read(_loadPct), NetworkLogColumns.LoadPct, rowNumber);
        if (loadResult.IsFailure) return Result.Failure<NetworkEvent>(loadResult.Error);

        Result<int?> latencyResult = NetworkLogColumns.ParseOptionalLatency(Read(_latencyMs), rowNumber);
        if (latencyResult.IsFailure) return Result.Failure<NetworkEvent>(latencyResult.Error);

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

    private static string? CellAsString(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        string raw = cell.GetString();
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    private static Result<DateTimeOffset> ParseTimestampFromCell(IXLCell cell, int rowNumber)
    {
        if (cell.IsEmpty())
        {
            return Result.Failure<DateTimeOffset>(
                NetworkLogErrors.MalformedRow(rowNumber, $"missing required column '{NetworkLogColumns.Timestamp}'"));
        }

        // ClosedXML promotes Excel date cells to DateTime; treat them as UTC instants.
        if (cell.DataType == XLDataType.DateTime)
        {
            DateTime dt = cell.GetDateTime();
            return Result.Success(new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)));
        }

        return NetworkLogColumns.ParseTimestamp(cell.GetString(), rowNumber);
    }
}
