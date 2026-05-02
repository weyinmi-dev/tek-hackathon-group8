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
    public PowerSource ActivePowerSource { get; private set; }
    public double FuelLevelLiters { get; private set; }
    public double FuelCapacityLiters { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static Tower Create(
        string code, string name, string region,
        double latitude, double longitude, double mapX, double mapY,
        int signalPct, int loadPct, TowerStatus status, string? issue,
        PowerSource activePowerSource = PowerSource.Grid,
        double fuelLevelLiters = 0, double fuelCapacityLiters = 1000)
    {
        var tower = new Tower(Guid.NewGuid(), code, name, region, latitude, longitude, mapX, mapY, signalPct, loadPct, status, issue);
        tower.ActivePowerSource = activePowerSource;
        tower.FuelLevelLiters = fuelLevelLiters;
        tower.FuelCapacityLiters = fuelCapacityLiters;
        return tower;
    }

    public void UpdateMetrics(int signalPct, int loadPct, TowerStatus status, string? issue)
    {
        SignalPct = signalPct;
        LoadPct = loadPct;
        Status = status;
        Issue = issue;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool UpdatePowerMetrics(PowerSource activePowerSource, double newFuelLevelLiters)
    {
        // Detect sudden unnatural drops (e.g. > 50 liters drop between readings)
        bool isTheftDetected = false;
        double oldFuelLevel = FuelLevelLiters;
        
        if (ActivePowerSource == PowerSource.Generator && (oldFuelLevel - newFuelLevelLiters) > 50)
        {
            isTheftDetected = true;
        }
        else if (ActivePowerSource != PowerSource.Generator && (oldFuelLevel - newFuelLevelLiters) > 10)
        {
            // If generator is OFF, fuel shouldn't drop at all
            isTheftDetected = true;
        }

        ActivePowerSource = activePowerSource;
        FuelLevelLiters = newFuelLevelLiters;
        UpdatedAtUtc = DateTime.UtcNow;

        if (isTheftDetected)
        {
            Raise(new Modules.Network.Domain.Towers.Events.FuelTheftDetectedDomainEvent(Id, Code, oldFuelLevel, newFuelLevelLiters));
        }

        return isTheftDetected;
    }
}

public interface ITowerRepository
{
    Task<IReadOnlyList<Tower>> ListAsync(CancellationToken cancellationToken = default);
    Task<Tower?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tower>> ListByRegionAsync(string region, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Tower> towers, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(Tower tower, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tower>> GetLowFuelTowersAsync(double fuelThresholdLiters, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tower>> GetActiveGeneratorTowersAsync(CancellationToken cancellationToken = default);
}
