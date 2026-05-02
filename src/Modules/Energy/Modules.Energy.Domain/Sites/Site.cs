using SharedKernel;

namespace Modules.Energy.Domain.Sites;

/// <summary>
/// A telecom base station with its energy mix. One Site per Tower (joined by SiteCode == Tower.Code).
/// State is mutated by the periodic ticker (battery drain, diesel burn, solar harvest) and by
/// operator commands (SwitchSource, RecordRefuel) — both go through the methods on this aggregate
/// so health derivation and audit logging stay consistent.
/// </summary>
public sealed class Site : Entity
{
    private Site(
        Guid id,
        string code,
        string name,
        string region,
        PowerSource source,
        int battPct,
        int dieselPct,
        double solarKw,
        bool gridUp,
        int dailyDieselLitres,
        long dailyCostNgn,
        double uptimePct,
        bool hasSolar,
        SiteHealth health,
        string? anomalyNote) : base(id)
    {
        Code = code;
        Name = name;
        Region = region;
        Source = source;
        BattPct = battPct;
        DieselPct = dieselPct;
        SolarKw = solarKw;
        GridUp = gridUp;
        DailyDieselLitres = dailyDieselLitres;
        DailyCostNgn = dailyCostNgn;
        UptimePct = uptimePct;
        HasSolar = hasSolar;
        Health = health;
        AnomalyNote = anomalyNote;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    private Site() { }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Region { get; private set; } = null!;
    public PowerSource Source { get; private set; }
    public int BattPct { get; private set; }
    public int DieselPct { get; private set; }
    public double SolarKw { get; private set; }
    public bool GridUp { get; private set; }
    public int DailyDieselLitres { get; private set; }
    public long DailyCostNgn { get; private set; }
    public double UptimePct { get; private set; }
    public bool HasSolar { get; private set; }
    public SiteHealth Health { get; private set; }
    public string? AnomalyNote { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static Site Create(
        string code, string name, string region,
        PowerSource source, int battPct, int dieselPct, double solarKw, bool gridUp,
        int dailyDieselLitres, long dailyCostNgn, double uptimePct, bool hasSolar,
        SiteHealth health, string? anomalyNote) =>
        new(Guid.NewGuid(), code, name, region, source, battPct, dieselPct, solarKw, gridUp,
            dailyDieselLitres, dailyCostNgn, uptimePct, hasSolar, health, anomalyNote);

    /// <summary>Operator action: change the active power source. Logged separately as a SiteEnergyLog row.</summary>
    public void SwitchSource(PowerSource newSource)
    {
        if (newSource == Source) return;
        Source = newSource;
        UpdatedAtUtc = DateTime.UtcNow;
        RecomputeHealth(hasOpenAnomaly: !string.IsNullOrEmpty(AnomalyNote));
    }

    /// <summary>Operator action: record a refuel — bumps diesel back toward full.</summary>
    public int RecordRefuel(int litresAdded)
    {
        // Treat 100% as a full ~120L tank for visual consistency with the seed pattern.
        int beforePct = DieselPct;
        DieselPct = Math.Clamp(DieselPct + (int)Math.Round(litresAdded * 0.83), 0, 100);
        UpdatedAtUtc = DateTime.UtcNow;
        RecomputeHealth(hasOpenAnomaly: !string.IsNullOrEmpty(AnomalyNote));
        return DieselPct - beforePct;
    }

    /// <summary>Periodic mutator. Drains battery / burns diesel / harvests solar based on the active source.</summary>
    public void ApplyTick(int seed, bool hasOpenAnomaly)
    {
        // Deterministic-ish drift from a per-tick seed so different sites diverge but
        // the same tick produces the same delta for a given site (helps debugging).
        int jitter = (seed % 7) - 3; // -3 .. +3
        switch (Source)
        {
            case PowerSource.Grid:
                // Grid sites top up battery and barely burn diesel.
                BattPct = Math.Clamp(BattPct + 1 + Math.Max(0, jitter / 2), 0, 100);
                DieselPct = Math.Clamp(DieselPct - (jitter > 1 ? 1 : 0), 0, 100);
                break;
            case PowerSource.Generator:
                // Diesel burns down quickly, battery holds.
                DieselPct = Math.Clamp(DieselPct - 2 - Math.Max(0, jitter / 2), 0, 100);
                BattPct = Math.Clamp(BattPct - (jitter > 2 ? 1 : 0), 0, 100);
                break;
            case PowerSource.Battery:
                // Battery drains; if there's solar, harvest a bit.
                int batteryDelta = HasSolar ? -1 + (jitter > 0 ? 1 : 0) : -2;
                BattPct = Math.Clamp(BattPct + batteryDelta, 0, 100);
                break;
            case PowerSource.Solar:
                // Solar harvest; battery gently charges, diesel idle.
                BattPct = Math.Clamp(BattPct + 2 + Math.Max(0, jitter / 2), 0, 100);
                SolarKw = Math.Round(Math.Clamp(SolarKw + (jitter * 0.05), 1.0, 8.0), 2);
                break;
        }

        UpdatedAtUtc = DateTime.UtcNow;
        RecomputeHealth(hasOpenAnomaly);
    }

    public void SetAnomalyNote(string? note)
    {
        AnomalyNote = note;
        UpdatedAtUtc = DateTime.UtcNow;
        RecomputeHealth(hasOpenAnomaly: !string.IsNullOrEmpty(note));
    }

    private void RecomputeHealth(bool hasOpenAnomaly) =>
        Health = SiteHealthExtensions.Derive(BattPct, DieselPct, Source, GridUp, hasOpenAnomaly);
}

public interface ISiteRepository
{
    Task<IReadOnlyList<Site>> ListAsync(CancellationToken ct = default);
    Task<Site?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Site?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Site> sites, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
