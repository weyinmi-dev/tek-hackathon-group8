using Application.Abstractions.Messaging;
using Modules.Ai.Application.Tools.Models;
using Modules.Network.Api;
using SharedKernel;

namespace Modules.Ai.Application.Tools.GetNetworkMetrics;

/// <summary>
/// Tool: GetNetworkMetrics. Returns current signal / load / status snapshot
/// for every tower in the named region. Backed by INetworkApi (in-process
/// cross-module contract) and dispatched through MediatR so the standard
/// pipeline (logging, validation, exception handling) wraps it just like
/// any other application query.
/// </summary>
public sealed record GetNetworkMetricsToolQuery(string Region) : IQuery<GetNetworkMetricsResult>;

internal sealed class GetNetworkMetricsToolHandler(INetworkApi network)
    : IQueryHandler<GetNetworkMetricsToolQuery, GetNetworkMetricsResult>
{
    public async Task<Result<GetNetworkMetricsResult>> Handle(GetNetworkMetricsToolQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<TowerSnapshot> rows = string.IsNullOrWhiteSpace(request.Region)
            ? await network.ListTowersAsync(cancellationToken)
            : await network.ListByRegionAsync(request.Region, cancellationToken);

        IReadOnlyList<TowerMetric> projected = rows
            .Select(t => new TowerMetric(t.Code, t.Name, t.Region, t.SignalPct, t.LoadPct, t.Status, t.Issue))
            .ToList();

        return Result.Success(new GetNetworkMetricsResult(request.Region ?? "all", projected.Count, projected));
    }
}
