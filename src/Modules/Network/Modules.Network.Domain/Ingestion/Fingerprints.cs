using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Modules.Network.Domain.Ingestion;

/// <summary>
/// Deterministic identifiers used to enforce idempotency across the ingestion pipeline.
/// <list type="bullet">
///   <item><see cref="ContentHash"/> dedups whole files: re-uploading the same bytes
///   resolves to an existing IngestionRun and short-circuits the pipeline.</item>
///   <item><see cref="AnomalyFingerprint"/> dedups individual anomalies inside a
///   bucketed time window so a follow-up ingestion that re-detects the same condition
///   updates the existing Alert (occurrence++) instead of creating a duplicate.</item>
/// </list>
/// Both are pure: same input → same hex digest, every time.
/// </summary>
public static class Fingerprints
{
    /// <summary>
    /// Default time bucket for anomaly deduplication. Two anomalies with the same
    /// (towerCode, type) collapse into the same fingerprint when their timestamps
    /// fall in the same 15-minute window.
    /// </summary>
    public static readonly TimeSpan DefaultAnomalyTimeBucket = TimeSpan.FromMinutes(15);

    public static string ContentHash(ReadOnlySpan<byte> content)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(content, hash);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Anomaly identity used by the decision layer to choose between CREATE and UPDATE
    /// of an Alert. The bucket is applied in UTC so timezone differences in the source
    /// data never split a single anomaly across two fingerprints.
    /// </summary>
    public static string AnomalyFingerprint(
        string towerCode,
        string anomalyType,
        DateTimeOffset detectedAt,
        TimeSpan? timeBucket = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(towerCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(anomalyType);

        TimeSpan bucket = timeBucket ?? DefaultAnomalyTimeBucket;
        if (bucket <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeBucket), bucket, "Bucket must be positive.");
        }

        long bucketTicks = bucket.Ticks;
        long detectedUtcTicks = detectedAt.UtcDateTime.Ticks;
        long bucketStartTicks = detectedUtcTicks - (detectedUtcTicks % bucketTicks);
        var bucketStart = new DateTime(bucketStartTicks, DateTimeKind.Utc);

        string canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{towerCode.Trim().ToUpperInvariant()}|{anomalyType.Trim().ToUpperInvariant()}|{bucketStart:O}");

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(canonical), hash);
        return Convert.ToHexString(hash);
    }
}
