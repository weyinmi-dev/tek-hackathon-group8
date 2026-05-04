using Application.Abstractions.Messaging;
using Modules.Energy.Domain.Sites;
using SharedKernel;

namespace Modules.Energy.Application.Optimization.Recommendations;

/// <summary>
/// Ranks concrete actions the operator can take to lower OPEX, derived from current Site state
/// (not hardcoded). Optionally narrowed to a single site for the MCP plugin's per-site path.
/// </summary>
public sealed record GetRecommendationsQuery(string? SiteCode = null) : IQuery<RecommendationsResponse>;

public sealed record RecommendationsResponse(IReadOnlyList<RecommendationDto> Recommendations);

public sealed record RecommendationDto(string Title, string Detail, string Tone, long EstimatedDailySavingsNgn);

internal sealed class GetRecommendationsQueryHandler(ISiteRepository sites)
    : IQueryHandler<GetRecommendationsQuery, RecommendationsResponse>
{
    public async Task<Result<RecommendationsResponse>> Handle(GetRecommendationsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Site> all = await sites.ListAsync(cancellationToken);
        IEnumerable<Site> scope = request.SiteCode is null
            ? all
            : all.Where(s => string.Equals(s.Code, request.SiteCode, StringComparison.OrdinalIgnoreCase));

        var list = new List<RecommendationDto>();

        Site[] highDieselNoSolar = scope.Where(s => !s.HasSolar && s.DailyDieselLitres >= 50).ToArray();
        if (highDieselNoSolar.Length > 0)
        {
            long savings = highDieselNoSolar.Sum(s => s.DailyCostNgn) * 25 / 100; // est. 25% reduction
            list.Add(new RecommendationDto(
                Title: $"Convert {highDieselNoSolar.Length} high-diesel sites to hybrid solar",
                Detail: string.Join(", ", highDieselNoSolar.Take(4).Select(s => s.Name)),
                Tone: "accent",
                EstimatedDailySavingsNgn: savings));
        }

        Site[] lowBattThreshold = scope.Where(s => s.BattPct < 70 && s.Source == PowerSource.Battery).ToArray();
        if (lowBattThreshold.Length > 0)
        {
            long savings = lowBattThreshold.Sum(s => s.DailyCostNgn) * 8 / 100;
            list.Add(new RecommendationDto(
                Title: $"Raise battery threshold 50→70% on {lowBattThreshold.Length} sites",
                Detail: "Reduces gen-cycle frequency by ~3 starts/day per site.",
                Tone: "accent",
                EstimatedDailySavingsNgn: savings));
        }

        Site[] criticalDiesel = scope.Where(s => s.DieselPct < 30).ToArray();
        if (criticalDiesel.Length > 0)
        {
            list.Add(new RecommendationDto(
                Title: $"Refuel {criticalDiesel.Length} sites within 4h",
                Detail: string.Join(", ", criticalDiesel.Take(4).Select(s => $"{s.Name} ({s.DieselPct}%)")),
                Tone: "warn",
                EstimatedDailySavingsNgn: 0));
        }

        Site[] degraded = scope.Where(s => s.Health == SiteHealth.Degraded).ToArray();
        if (degraded.Length > 0)
        {
            list.Add(new RecommendationDto(
                Title: $"Investigate {degraded.Length} degraded sites",
                Detail: string.Join(", ", degraded.Take(4).Select(s => s.Name)),
                Tone: "info",
                EstimatedDailySavingsNgn: 0));
        }

        if (list.Count == 0)
        {
            list.Add(new RecommendationDto(
                Title: "Fleet healthy — no actionable optimizations",
                Detail: "Re-run after the next ticker pass for fresh recommendations.",
                Tone: "info",
                EstimatedDailySavingsNgn: 0));
        }

        return Result.Success(new RecommendationsResponse(list));
    }
}
