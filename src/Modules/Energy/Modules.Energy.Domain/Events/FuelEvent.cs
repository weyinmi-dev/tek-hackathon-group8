using SharedKernel;

namespace Modules.Energy.Domain.Events;

public enum FuelEventKind
{
    Refuel = 0,
    Consumption = 1,
    Theft = 2,
    SensorOffline = 3,
}

/// <summary>
/// Append-only fuel-level event log. The diesel trace and theft detection both feed off this.
/// </summary>
public sealed class FuelEvent : Entity
{
    private FuelEvent(Guid id, string siteCode, FuelEventKind kind, int litresDelta, int dieselPctAfter, string? note, DateTime occurredAtUtc)
        : base(id)
    {
        SiteCode = siteCode;
        Kind = kind;
        LitresDelta = litresDelta;
        DieselPctAfter = dieselPctAfter;
        Note = note;
        OccurredAtUtc = occurredAtUtc;
    }

    private FuelEvent() { }

    public string SiteCode { get; private set; } = null!;
    public FuelEventKind Kind { get; private set; }
    public int LitresDelta { get; private set; }
    public int DieselPctAfter { get; private set; }
    public string? Note { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    public static FuelEvent Record(string siteCode, FuelEventKind kind, int litresDelta, int dieselPctAfter, string? note = null) =>
        new(Guid.NewGuid(), siteCode, kind, litresDelta, dieselPctAfter, note, DateTime.UtcNow);
}

public interface IFuelEventRepository
{
    Task AddAsync(FuelEvent ev, CancellationToken ct = default);
    Task<IReadOnlyList<FuelEvent>> ListForSiteAsync(string siteCode, int hours, CancellationToken ct = default);
    Task<int> CountSinceAsync(DateTime sinceUtc, FuelEventKind kind, CancellationToken ct = default);
}
