using System.Globalization;
using Modules.Network.Application.Ingestion.Stage1_Ingest;
using SharedKernel;

namespace Modules.Network.Infrastructure.Ingestion.Parsers;

/// <summary>
/// Canonical column names + reusable cell-level parsers shared across the four
/// file-format parsers. Names are case-insensitive when matched against headers
/// so callers can upload sources with either snake_case or PascalCase headers.
/// </summary>
internal static class NetworkLogColumns
{
    public const string Timestamp = "timestamp";
    public const string TowerCode = "tower_code";
    public const string SignalPct = "signal_pct";
    public const string LoadPct = "load_pct";
    public const string LatencyMs = "latency_ms";
    public const string Status = "status";

    public static IReadOnlyList<string> Required { get; } = [Timestamp, TowerCode];

    public static bool MatchesHeader(string header, string canonical) =>
        header.Trim().Replace(" ", "_", StringComparison.Ordinal)
            .Equals(canonical, StringComparison.OrdinalIgnoreCase);

    public static Result<DateTimeOffset> ParseTimestamp(string? raw, int rowNumber)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result.Failure<DateTimeOffset>(
                NetworkLogErrors.MalformedRow(rowNumber, $"missing required column '{Timestamp}'"));
        }

        if (DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            return Result.Success(parsed);
        }

        return Result.Failure<DateTimeOffset>(
            NetworkLogErrors.MalformedRow(rowNumber, $"unparseable {Timestamp} '{raw}'"));
    }

    public static Result<int?> ParseOptionalPercent(string? raw, string column, int rowNumber)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result.Success<int?>(null);
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return Result.Failure<int?>(
                NetworkLogErrors.MalformedRow(rowNumber, $"non-integer {column} '{raw}'"));
        }

        if (value is < 0 or > 100)
        {
            return Result.Failure<int?>(
                NetworkLogErrors.MalformedRow(rowNumber, $"{column}={value} outside 0..100"));
        }

        return Result.Success<int?>(value);
    }

    public static Result<int?> ParseOptionalLatency(string? raw, int rowNumber)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result.Success<int?>(null);
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            return Result.Failure<int?>(
                NetworkLogErrors.MalformedRow(rowNumber, $"non-integer {LatencyMs} '{raw}'"));
        }

        if (value < 0)
        {
            return Result.Failure<int?>(
                NetworkLogErrors.MalformedRow(rowNumber, $"{LatencyMs}={value} cannot be negative"));
        }

        return Result.Success<int?>(value);
    }

    public static Result<string> ParseTowerCode(string? raw, int rowNumber)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result.Failure<string>(
                NetworkLogErrors.MalformedRow(rowNumber, $"missing required column '{TowerCode}'"));
        }

        return Result.Success(raw);
    }
}
