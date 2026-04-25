using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;

namespace Modules.Network.Application.Map.GetMap;

public sealed record GetMapQuery() : IQuery<MapResponse>, ICachedQuery
{
    public string CacheKey => "map:lagos";
    public TimeSpan? Expiration => TimeSpan.FromSeconds(15);
}

public sealed record MapResponse(
    IReadOnlyList<TowerDto> Towers,
    IReadOnlyList<RegionHealthDto> Regions,
    int TotalTowers,
    int OnlineTowers);

public sealed record TowerDto(
    string Id,
    string Name,
    string Region,
    double Lat,
    double Lng,
    double X,
    double Y,
    int Signal,
    int Load,
    string Status,
    string? Issue);

public sealed record RegionHealthDto(
    string Name,
    int Towers,
    int Critical,
    int Warn,
    int AvgSignal);
