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

    /// <summary>
    /// Creates a tower from an OSS site snapshot. The provider reports real coordinates but not a
    /// position on our abstract canvas, so the map placement is projected from them.
    ///
    /// This factory exists because a snapshot — unlike the analyzer — <i>is</i> allowed to bring a
    /// tower into existence. The decision engine deliberately refuses to: an AI inferring a tower
    /// that isn't there would corrupt the topology. An operator feed reporting a site it owns is
    /// the authoritative source for that site, so a new site code means a new tower.
    /// </summary>
    public static Tower CreateFromSnapshot(
        string code, string name, string region,
        double latitude, double longitude,
        int signalPct, int loadPct, TowerStatus status, string? issue)
        => new(
            Guid.NewGuid(),
            code.Trim().ToUpperInvariant(),
            name,
            region,
            latitude,
            longitude,
            LagosMapProjection.MapX(longitude),
            LagosMapProjection.MapY(latitude),
            signalPct,
            loadPct,
            status,
            issue);

    public void UpdateMetrics(int signalPct, int loadPct, TowerStatus status, string? issue)
    {
        SignalPct = signalPct;
        LoadPct = loadPct;
        Status = status;
        Issue = issue;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies the identity and location a snapshot reports. Separate from <see cref="UpdateMetrics"/>
    /// because the two have different authorities: any pipeline run may revise a tower's live metrics,
    /// but only an operator feed may rename or move it. Returns true when something actually changed,
    /// so a re-upload of an unchanged document is reported as a no-op rather than a false update.
    /// </summary>
    public bool ApplyIdentity(string name, string region, double? latitude, double? longitude)
    {
        bool moved = latitude is double lat && longitude is double lon &&
                     (Math.Abs(Latitude - lat) > CoordinateEpsilon || Math.Abs(Longitude - lon) > CoordinateEpsilon);

        bool renamed =
            !string.Equals(Name, name, StringComparison.Ordinal) ||
            !string.Equals(Region, region, StringComparison.Ordinal);

        if (!moved && !renamed)
        {
            return false;
        }

        Name = name;
        Region = region;

        if (moved)
        {
            Latitude = latitude!.Value;
            Longitude = longitude!.Value;
            MapX = LagosMapProjection.MapX(Longitude);
            MapY = LagosMapProjection.MapY(Latitude);
        }

        UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// ~1 m at Lagos' latitude. Coordinates are floating point and GPS jitters; without a tolerance
    /// every re-upload would look like the tower had moved.
    /// </summary>
    private const double CoordinateEpsilon = 0.00001;
}

public interface ITowerRepository
{
    Task<IReadOnlyList<Tower>> ListAsync(CancellationToken cancellationToken = default);
    Task<Tower?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tower>> ListByRegionAsync(string region, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a tower for mutation, tracked by the unit of work.
    ///
    /// <see cref="GetByCodeAsync"/> is a read: it returns a detached instance, which is right for
    /// query paths and wrong for anything that intends to write. Mutating a detached tower and
    /// calling SaveChanges is a silent no-op — the pipeline did exactly that, reporting tower
    /// updates it never persisted. Write paths must come through here.
    /// </summary>
    Task<Tower?> GetForUpdateAsync(string code, CancellationToken cancellationToken = default);

    Task AddAsync(Tower tower, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Tower> towers, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
