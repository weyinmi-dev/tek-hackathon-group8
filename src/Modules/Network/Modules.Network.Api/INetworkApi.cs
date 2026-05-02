namespace Modules.Network.Api;

public sealed record TowerSnapshot(
    string Code,
    string Name,
    string Region,
    int SignalPct,
    int LoadPct,
    string Status,
    string? Issue,
    double Latitude = 0,
    double Longitude = 0);

public sealed record RegionHealth(string Region, int TowerCount, int CriticalCount, int WarnCount, int AvgSignalPct);

public interface INetworkApi
{
    Task<IReadOnlyList<TowerSnapshot>> ListTowersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TowerSnapshot>> ListByRegionAsync(string region, CancellationToken cancellationToken = default);
    Task<TowerSnapshot?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegionHealth>> GetRegionHealthAsync(CancellationToken cancellationToken = default);
}
