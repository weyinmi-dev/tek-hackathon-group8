using SharedKernel;

namespace Modules.Energy.Domain.Sites;

/// <summary>
/// One row per site capturing battery state of health — capacity %, total cycles,
/// and a projected end-of-life date computed from cycle rate. Updated alongside Site
/// telemetry but kept separate so we can age it independently and feed RAG with it.
/// </summary>
public sealed class BatteryHealth : Entity
{
    private BatteryHealth(Guid id, string siteCode, double capacityPct, int cycleCount, DateTime? eolProjectedUtc)
        : base(id)
    {
        SiteCode = siteCode;
        CapacityPct = capacityPct;
        CycleCount = cycleCount;
        EolProjectedUtc = eolProjectedUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private BatteryHealth() { }

    public string SiteCode { get; private set; } = null!;
    public double CapacityPct { get; private set; }
    public int CycleCount { get; private set; }
    public DateTime? EolProjectedUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static BatteryHealth Create(string siteCode, double capacityPct, int cycleCount, DateTime? eolProjectedUtc) =>
        new(Guid.NewGuid(), siteCode, capacityPct, cycleCount, eolProjectedUtc);

    public void RecordCycle()
    {
        CycleCount++;
        // Capacity fades ~0.005% per cycle (very gentle). EOL is when capacity dips below 75%.
        CapacityPct = Math.Max(60, CapacityPct - 0.005);
        if (CapacityPct < 75 && EolProjectedUtc is null)
        {
            EolProjectedUtc = DateTime.UtcNow.AddDays(30);
        }
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public interface IBatteryHealthRepository
{
    Task<BatteryHealth?> GetForSiteAsync(string siteCode, CancellationToken ct = default);
    Task<IReadOnlyList<BatteryHealth>> ListAsync(CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<BatteryHealth> rows, CancellationToken ct = default);
}
