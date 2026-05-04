using Modules.Network.Api;
using Modules.Network.Domain.Towers;

namespace Modules.Network.Infrastructure.Api;

internal sealed class NetworkApi(ITowerRepository towers) : INetworkApi
{
    public async Task<IReadOnlyList<TowerSnapshot>> ListTowersAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Tower> all = await towers.ListAsync(ct);
        return all.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<TowerSnapshot>> ListByRegionAsync(string region, CancellationToken ct = default)
    {
        IReadOnlyList<Tower> rows = await towers.ListByRegionAsync(region, ct);
        return rows.Select(Map).ToList();
    }

    public async Task<TowerSnapshot?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        Tower? t = await towers.GetByCodeAsync(code, ct);
        return t is null ? null : Map(t);
    }

    public async Task<IReadOnlyList<RegionHealth>> GetRegionHealthAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Tower> all = await towers.ListAsync(ct);
        return all.GroupBy(t => t.Region)
            .Select(g => new RegionHealth(
                g.Key,
                g.Count(),
                g.Count(t => t.Status == TowerStatus.Critical),
                g.Count(t => t.Status == TowerStatus.Warn),
                (int)Math.Round(g.Average(t => t.SignalPct))))
            .OrderBy(r => r.Region)
            .ToList();
    }

    private static TowerSnapshot Map(Tower t) =>
        new(t.Code, t.Name, t.Region, t.SignalPct, t.LoadPct, t.Status.ToWire(), t.Issue, t.Latitude, t.Longitude);
}
