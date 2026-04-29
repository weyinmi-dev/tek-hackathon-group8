using Application.Abstractions.Messaging;
using Modules.Ai.Application.Tools.Models;
using Modules.Alerts.Api;
using SharedKernel;

namespace Modules.Ai.Application.Tools.GetOutages;

/// <summary>
/// Tool: GetOutages. Returns active (or, if AllStatuses=true, all) incidents
/// optionally narrowed to a region.
/// </summary>
public sealed record GetOutagesToolQuery(string? Region = null, bool AllStatuses = false) : IQuery<GetOutagesResult>;

internal sealed class GetOutagesToolHandler(IAlertsApi alerts)
    : IQueryHandler<GetOutagesToolQuery, GetOutagesResult>
{
    public async Task<Result<GetOutagesResult>> Handle(GetOutagesToolQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<AlertSnapshot> rows = request.AllStatuses
            ? await alerts.ListAllAsync(cancellationToken)
            : await alerts.ListActiveAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Region))
        {
            rows = rows
                .Where(r => string.Equals(r.Region, request.Region, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        IReadOnlyList<OutageRow> projected = rows
            .Select(r => new OutageRow(
                r.Code, r.Severity, r.Status, r.Region, r.TowerCode,
                r.Cause, r.SubscribersAffected, r.Confidence))
            .ToList();

        return Result.Success(new GetOutagesResult(projected.Count, projected));
    }
}
