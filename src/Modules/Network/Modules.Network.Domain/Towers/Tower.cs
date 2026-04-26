using SharedKernel;

namespace Modules.Network.Domain.Towers;

public sealed class Tower : Entity
{
    private Tower(
        Guid id,
        string code,
        string name,
        string region,
        double latitude,
        double longitude,
        double mapX,
        double mapY,
        int signalPct,
        int loadPct,
        TowerStatus status,
        string? issue) : base(id)
    {
        Code = code;
        Name = name;
        Region = region;
        Latitude = latitude;
        Longitude = longitude;
        MapX = mapX;
        MapY = mapY;
        SignalPct = signalPct;
        LoadPct = loadPct;
        Status = status;
        Issue = issue;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private Tower() { }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Region { get; private set; } = null!;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double MapX { get; private set; }
    public double MapY { get; private set; }
    public int SignalPct { get; private set; }
    public int LoadPct { get; private set; }
    public TowerStatus Status { get; private set; }
    public string? Issue { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static Tower Create(
        string code, string name, string region,
        double latitude, double longitude, double mapX, double mapY,
        int signalPct, int loadPct, TowerStatus status, string? issue)
        => new(Guid.NewGuid(), code, name, region, latitude, longitude, mapX, mapY, signalPct, loadPct, status, issue);

    public void UpdateMetrics(int signalPct, int loadPct, TowerStatus status, string? issue)
    {
        SignalPct = signalPct;
        LoadPct = loadPct;
        Status = status;
        Issue = issue;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public interface ITowerRepository
{
    Task<IReadOnlyList<Tower>> ListAsync(CancellationToken cancellationToken = default);
    Task<Tower?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tower>> ListByRegionAsync(string region, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Tower> towers, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
