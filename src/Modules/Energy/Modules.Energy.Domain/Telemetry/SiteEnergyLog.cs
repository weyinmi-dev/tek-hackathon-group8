using SharedKernel;

namespace Modules.Energy.Domain.Telemetry;

/// <summary>
/// Append-only telemetry snapshot taken on every ticker pass. Drives the 24h diesel
/// trace, OPEX projection, and any RAG indexing of "what was happening at this site".
/// </summary>
public sealed class SiteEnergyLog : Entity
{
    private SiteEnergyLog(
        Guid id, string siteCode, DateTime recordedAtUtc,
        int battPct, int dieselPct, double solarKw, bool gridUp,
        int activeSourceCode, long costNgnDelta) : base(id)
    {
        SiteCode = siteCode;
        RecordedAtUtc = recordedAtUtc;
        BattPct = battPct;
        DieselPct = dieselPct;
        SolarKw = solarKw;
        GridUp = gridUp;
        ActiveSourceCode = activeSourceCode;
        CostNgnDelta = costNgnDelta;
    }

    private SiteEnergyLog() { }

    public string SiteCode { get; private set; } = null!;
    public DateTime RecordedAtUtc { get; private set; }
    public int BattPct { get; private set; }
    public int DieselPct { get; private set; }
    public double SolarKw { get; private set; }
    public bool GridUp { get; private set; }
    public int ActiveSourceCode { get; private set; }
    public long CostNgnDelta { get; private set; }

    /// <summary>Live tick: the reading is being taken right now, so "now" is the correct stamp.</summary>
    public static SiteEnergyLog Snapshot(
        string siteCode, int battPct, int dieselPct, double solarKw, bool gridUp,
        int activeSourceCode, long costNgnDelta) =>
        new(Guid.NewGuid(), siteCode, DateTime.UtcNow, battPct, dieselPct, solarKw, gridUp,
            activeSourceCode, costNgnDelta);

    /// <summary>
    /// Reported reading: the measurement was taken when the provider says it was, which is not when
    /// we happened to receive the file. Stamping an ingested snapshot with the wall clock would file
    /// yesterday's readings under today and bend every trend that reads this table — and would make
    /// a backfill of historical snapshots land as a vertical spike at the moment of upload.
    /// </summary>
    public static SiteEnergyLog Reported(
        string siteCode, DateTime recordedAtUtc, int battPct, int dieselPct, double solarKw, bool gridUp,
        int activeSourceCode, long costNgnDelta) =>
        new(Guid.NewGuid(), siteCode, DateTime.SpecifyKind(recordedAtUtc, DateTimeKind.Utc),
            battPct, dieselPct, solarKw, gridUp, activeSourceCode, costNgnDelta);
}

public interface ISiteEnergyLogRepository
{
    Task AddRangeAsync(IEnumerable<SiteEnergyLog> rows, CancellationToken ct = default);
    Task<IReadOnlyList<SiteEnergyLog>> ListForSiteAsync(string siteCode, int hours, CancellationToken ct = default);
    Task<IReadOnlyList<SiteEnergyLog>> ListSinceAsync(DateTime sinceUtc, int max, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
