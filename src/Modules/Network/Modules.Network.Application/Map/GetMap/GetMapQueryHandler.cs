using Application.Abstractions.Messaging;
using Modules.Network.Domain.Towers;
using SharedKernel;

namespace Modules.Network.Application.Map.GetMap;

internal sealed class GetMapQueryHandler(ITowerRepository towers)
    : IQueryHandler<GetMapQuery, MapResponse>
{
    public async Task<Result<MapResponse>> Handle(GetMapQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Tower> all = await towers.ListAsync(cancellationToken);

        IReadOnlyList<TowerDto> towerDtos = all
            .Select(t => new TowerDto(
                t.Code, t.Name, t.Region, t.Latitude, t.Longitude, t.MapX, t.MapY,
                t.SignalPct, t.LoadPct, t.Status.ToWire(), t.Issue))
            .ToList();

        IReadOnlyList<RegionHealthDto> regions = all
            .GroupBy(t => t.Region)
            .Select(g => new RegionHealthDto(
                g.Key,
                g.Count(),
                g.Count(t => t.Status == TowerStatus.Critical),
                g.Count(t => t.Status == TowerStatus.Warn),
                (int)Math.Round(g.Average(t => t.SignalPct))))
            .OrderBy(r => r.Name)
            .ToList();

        int online = all.Count(t => t.Status != TowerStatus.Critical);

        return Result.Success(new MapResponse(towerDtos, regions, all.Count, online));
    }
}
