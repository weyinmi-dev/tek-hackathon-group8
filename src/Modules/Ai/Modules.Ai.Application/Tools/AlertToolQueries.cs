using Application.Abstractions.Messaging;
using Modules.Alerts.Api;
using SharedKernel;

namespace Modules.Ai.Application.Tools;

// Canonical Alert tool queries (Phase 2 §6.2) over IAlertsApi.

/// <summary>get_active_outages — active/recent incidents across the metro.</summary>
public sealed record GetActiveOutagesQuery() : IQuery<IReadOnlyList<AlertSnapshot>>;

internal sealed class GetActiveOutagesQueryHandler(IAlertsApi alerts)
    : IQueryHandler<GetActiveOutagesQuery, IReadOnlyList<AlertSnapshot>>
{
    public async Task<Result<IReadOnlyList<AlertSnapshot>>> Handle(GetActiveOutagesQuery request, CancellationToken cancellationToken)
        => Result.Success(await alerts.ListActiveAsync(cancellationToken));
}

/// <summary>search_alarm_history — all alerts (active + resolved), optionally filtered to a region.</summary>
public sealed record SearchAlarmHistoryQuery(string? Region = null) : IQuery<IReadOnlyList<AlertSnapshot>>;

internal sealed class SearchAlarmHistoryQueryHandler(IAlertsApi alerts)
    : IQueryHandler<SearchAlarmHistoryQuery, IReadOnlyList<AlertSnapshot>>
{
    public async Task<Result<IReadOnlyList<AlertSnapshot>>> Handle(SearchAlarmHistoryQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<AlertSnapshot> all = await alerts.ListAllAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Region))
        {
            return Result.Success(all);
        }
        string region = request.Region.Trim();
        IReadOnlyList<AlertSnapshot> filtered = all
            .Where(a => a.Region.Contains(region, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Result.Success(filtered);
    }
}
