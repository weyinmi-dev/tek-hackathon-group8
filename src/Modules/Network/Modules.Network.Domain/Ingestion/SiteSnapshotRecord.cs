using SharedKernel;

namespace Modules.Network.Domain.Ingestion;

/// <summary>
/// One full OSS site snapshot as it arrived, owned by the <see cref="IngestionRun"/> that
/// carried it. Like <see cref="NetworkEvent"/> this is immutable after Stage 1 — it is the
/// evidence of what the feed said, so later stages read it and never edit it.
///
/// It serves three jobs at once, which is why the columns look redundant against
/// <see cref="RawJson"/>:
///   1. <b>Historical telemetry</b> — the append-only record of every state a site has been
///      reported in, queryable by site and time without deserialising anything.
///   2. <b>The file index</b> — the flattened, searchable metadata (provider, environment,
///      region, vendor, technologies, hash) that global search, Copilot, and audit read.
///   3. <b>The stage hand-off</b> — Stage 3 rehydrates the typed payload from RawJson, the
///      same way Stage 2 rehydrates events from the events table. Keeping the canonical
///      document verbatim means a re-run of a stage sees exactly what the first run saw.
///
/// The domain deliberately holds the payload as a string. The typed shape
/// (<c>SiteSnapshotPayload</c>) lives in the shared pipeline abstractions, which
/// Network.Domain does not — and must not — reference.
/// </summary>
public sealed class SiteSnapshotRecord : Entity
{
    private SiteSnapshotRecord(
        Guid id,
        Guid ingestionRunId,
        string requestId,
        string provider,
        string environment,
        string siteId,
        string siteCode,
        string siteName,
        string region,
        string? vendor,
        string technologies,
        int? healthScore,
        double? latitude,
        double? longitude,
        DateTimeOffset generatedAt,
        DateTimeOffset? capturedAt,
        DateTimeOffset? lastHeartbeat,
        int snapshotVersion,
        string rawJson) : base(id)
    {
        IngestionRunId = ingestionRunId;
        RequestId = requestId;
        Provider = provider;
        Environment = environment;
        SiteId = siteId;
        SiteCode = siteCode;
        SiteName = siteName;
        Region = region;
        Vendor = vendor;
        Technologies = technologies;
        HealthScore = healthScore;
        Latitude = latitude;
        Longitude = longitude;
        GeneratedAt = generatedAt;
        CapturedAt = capturedAt;
        LastHeartbeat = lastHeartbeat;
        SnapshotVersion = snapshotVersion;
        RawJson = rawJson;
    }

    private SiteSnapshotRecord() { }

    public Guid IngestionRunId { get; private set; }

    /// <summary>Upstream correlation id from the feed. Not unique across providers, so never a key.</summary>
    public string RequestId { get; private set; } = null!;

    public string Provider { get; private set; } = null!;
    public string Environment { get; private set; } = null!;
    public string SiteId { get; private set; } = null!;

    /// <summary>The join key to <c>Tower.Code</c> and Energy's <c>Site.Code</c>. Upper-invariant.</summary>
    public string SiteCode { get; private set; } = null!;

    public string SiteName { get; private set; } = null!;
    public string Region { get; private set; } = null!;
    public string? Vendor { get; private set; }

    /// <summary>Comma-separated technology list ("2G,3G,4G,5G") — flattened for indexing and search.</summary>
    public string Technologies { get; private set; } = null!;

    public int? HealthScore { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    /// <summary>When the provider generated the document.</summary>
    public DateTimeOffset GeneratedAt { get; private set; }

    /// <summary>When the performance measurements inside it were taken. The telemetry timestamp.</summary>
    public DateTimeOffset? CapturedAt { get; private set; }

    public DateTimeOffset? LastHeartbeat { get; private set; }
    public int SnapshotVersion { get; private set; }
    public string RawJson { get; private set; } = null!;

    public static SiteSnapshotRecord Create(
        Guid ingestionRunId,
        string requestId,
        string provider,
        string environment,
        string siteId,
        string siteCode,
        string siteName,
        string region,
        string? vendor,
        string technologies,
        int? healthScore,
        double? latitude,
        double? longitude,
        DateTimeOffset generatedAt,
        DateTimeOffset? capturedAt,
        DateTimeOffset? lastHeartbeat,
        int snapshotVersion,
        string rawJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawJson);

        return new SiteSnapshotRecord(
            Guid.NewGuid(),
            ingestionRunId,
            requestId,
            provider,
            environment,
            siteId,
            siteCode.Trim().ToUpperInvariant(),
            siteName,
            region,
            vendor,
            technologies,
            healthScore,
            latitude,
            longitude,
            generatedAt,
            capturedAt,
            lastHeartbeat,
            snapshotVersion,
            rawJson);
    }
}
