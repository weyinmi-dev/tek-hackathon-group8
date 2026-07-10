using Application.Abstractions.Messaging;
using Modules.Network.Api;
using SharedKernel;

namespace Modules.Ai.Application.Tools;

// The canonical Network tool queries (Phase 2 §6.2). Each is a thin MediatR query over
// INetworkApi so the M6 agent tools dispatch through the standard application pipeline.
// Results are the cross-module .Api snapshots directly — the tool serializes them for the model.

/// <summary>get_region_metrics — signal/load/status for every tower in a region (empty = all).</summary>
public sealed record GetRegionMetricsQuery(string Region) : IQuery<IReadOnlyList<TowerSnapshot>>;

internal sealed class GetRegionMetricsQueryHandler(INetworkApi network)
    : IQueryHandler<GetRegionMetricsQuery, IReadOnlyList<TowerSnapshot>>
{
    public async Task<Result<IReadOnlyList<TowerSnapshot>>> Handle(GetRegionMetricsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<TowerSnapshot> towers = string.IsNullOrWhiteSpace(request.Region)
            ? await network.ListTowersAsync(cancellationToken)
            : await network.ListByRegionAsync(request.Region, cancellationToken);
        return Result.Success(towers);
    }
}

/// <summary>get_tower_metrics — current snapshot for a single tower by code.</summary>
public sealed record GetTowerMetricsQuery(string TowerCode) : IQuery<TowerSnapshot>;

internal sealed class GetTowerMetricsQueryHandler(INetworkApi network)
    : IQueryHandler<GetTowerMetricsQuery, TowerSnapshot>
{
    public async Task<Result<TowerSnapshot>> Handle(GetTowerMetricsQuery request, CancellationToken cancellationToken)
    {
        TowerSnapshot? tower = await network.GetByCodeAsync(request.TowerCode, cancellationToken);
        return tower is null
            ? Result.Failure<TowerSnapshot>(Error.NotFound("Tower.NotFound", $"No tower with code '{request.TowerCode}'."))
            : Result.Success(tower);
    }
}

/// <summary>search_towers — free-text match over tower code, name or region.</summary>
public sealed record SearchTowersQuery(string Query) : IQuery<IReadOnlyList<TowerSnapshot>>;

internal sealed class SearchTowersQueryHandler(INetworkApi network)
    : IQueryHandler<SearchTowersQuery, IReadOnlyList<TowerSnapshot>>
{
    public async Task<Result<IReadOnlyList<TowerSnapshot>>> Handle(SearchTowersQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<TowerSnapshot> all = await network.ListTowersAsync(cancellationToken);
        string q = (request.Query ?? string.Empty).Trim();
        if (q.Length == 0)
        {
            return Result.Success(all);
        }
        IReadOnlyList<TowerSnapshot> matches = all
            .Where(t => t.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || t.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || t.Region.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Result.Success(matches);
    }
}
