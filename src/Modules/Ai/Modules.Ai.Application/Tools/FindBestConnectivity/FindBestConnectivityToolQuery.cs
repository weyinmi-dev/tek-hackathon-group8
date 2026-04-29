using Application.Abstractions.Messaging;
using Modules.Ai.Application.Tools.Models;
using Modules.Network.Api;
using SharedKernel;

namespace Modules.Ai.Application.Tools.FindBestConnectivity;

/// <summary>
/// Tool: FindBestConnectivity. Ranks healthy towers in a region for
/// load-shed / failover targeting (highest signal, lowest load, online status).
/// </summary>
public sealed record FindBestConnectivityToolQuery(string Region, int Limit = 3) : IQuery<FindBestConnectivityResult>;

internal sealed class FindBestConnectivityToolHandler(INetworkApi network)
    : IQueryHandler<FindBestConnectivityToolQuery, FindBestConnectivityResult>
{
    public async Task<Result<FindBestConnectivityResult>> Handle(FindBestConnectivityToolQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<TowerSnapshot> rows = string.IsNullOrWhiteSpace(request.Region)
            ? await network.ListTowersAsync(cancellationToken)
            : await network.ListByRegionAsync(request.Region, cancellationToken);

        int limit = Math.Clamp(request.Limit, 1, 10);

        IReadOnlyList<ConnectivityRecommendation> ranked = rows
            .Where(t => !string.Equals(t.Status, "offline", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(t => t.SignalPct)
            .ThenBy(t => t.LoadPct)
            .Take(limit)
            .Select(t => new ConnectivityRecommendation(
                t.Code, t.Name, t.Region, t.SignalPct, t.LoadPct, t.Status,
                Reason: $"signal {t.SignalPct}% / load {t.LoadPct}% — capacity headroom {100 - t.LoadPct}%"))
            .ToList();

        return Result.Success(new FindBestConnectivityResult(request.Region ?? "all", ranked));
    }
}
