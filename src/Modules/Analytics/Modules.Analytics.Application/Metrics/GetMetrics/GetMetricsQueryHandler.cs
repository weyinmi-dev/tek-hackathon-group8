using System.Globalization;
using Application.Abstractions.Messaging;
using Modules.Alerts.Api;
using Modules.Analytics.Domain.Audit;
using Modules.Network.Api;
using SharedKernel;

namespace Modules.Analytics.Application.Metrics.GetMetrics;

internal sealed class GetMetricsQueryHandler(
    INetworkApi network,
    IAlertsApi alertsApi,
    IAuditRepository audit)
    : IQueryHandler<GetMetricsQuery, MetricsResponse>
{
    // Sparks are pre-baked, demo-grade traces. They mirror what the design-system
    // shows on the Command Center and Dashboard. Real production code would compute
    // them from a time-series store; for the hackathon we want the visual punch
    // without standing up Prometheus.
    private static readonly SparkSeries Sparks = new(
        Uptime:   [99.92,99.91,99.92,99.93,99.92,99.90,99.91,99.92,99.90,99.88,99.87,99.85,99.85,99.84,99.85],
        Latency:  [34,35,33,34,36,35,38,40,42,44,46,42,41,42,42],
        Incident: [7,8,8,9,10,10,11,11,12,12,13,14,14,14,14],
        Towers:   [1289,1289,1290,1290,1291,1291,1290,1289,1289,1287,1286,1285,1285,1284,1284],
        Subs:     [10,12,14,18,22,28,30,34,40,44,48,50,52,52,52],
        Queries:  [800,900,1000,1100,1300,1500,1700,1900,2100,2300,2500,2700,2800,2820,2841]);

    public async Task<Result<MetricsResponse>> Handle(GetMetricsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<TowerSnapshot> towers = await network.ListTowersAsync(cancellationToken);
        IReadOnlyList<RegionHealth> regionHealth = await network.GetRegionHealthAsync(cancellationToken);
        IReadOnlyList<AlertSnapshot> active = await alertsApi.ListActiveAsync(cancellationToken);

        int crit = active.Count(a => a.Severity == "critical");
        int warn = active.Count(a => a.Severity == "warn");
        int info = active.Count(a => a.Severity == "info");
        int affectedSubs = active.Sum(a => a.SubscribersAffected);
        int totalTowers = towers.Count > 0 ? towers.Count + 1276 : 1291; // pad to fleet size for demo
        int onlineTowers = totalTowers - towers.Count(t => t.Status == "critical");

        var kpis = new List<KpiCard>
        {
            new("Network Uptime",        "99.847", "%",       "-0.03",                                "down", "24h rolling"),
            new("Avg Latency",           "42",     "ms",      "+8",                                   "down", "p95 across LAG metro"),
            new("Active Incidents",      active.Count.ToString(CultureInfo.InvariantCulture), "",  $"+{crit}", "down", $"{crit} critical, {warn} warn, {info} info"),
            new("Towers Online",         $"{onlineTowers:N0}", $"/ {totalTowers:N0}", "-3",          "down", "Lagos metro"),
            new("Subscribers Affected",  $"{affectedSubs / 1000.0:F1}", "K", "+14.2K",               "down", "last 60 min"),
            new("Copilot Queries",       "2,841",  "",        "+412",                                 "up",   "today"),
        };

        IReadOnlyList<RegionHealthMetric> regions = regionHealth
            .Select(r => new RegionHealthMetric(
                Name: r.Region,
                AvgSignal: r.AvgSignalPct,
                Tone: r.AvgSignalPct switch
                {
                    > 75 => "ok",
                    > 50 => "warn",
                    _    => "crit",
                }))
            .ToList();

        IReadOnlyList<IncidentTypeBreakdown> types =
        [
            new("Fiber cut",     active.Count(a => a.Cause.Contains("fiber", StringComparison.OrdinalIgnoreCase)) + 8),
            new("Power outage",  active.Count(a => a.Cause.Contains("power", StringComparison.OrdinalIgnoreCase) || a.Cause.Contains("grid", StringComparison.OrdinalIgnoreCase)) + 12),
            new("Congestion",    active.Count(a => a.Cause.Contains("congest", StringComparison.OrdinalIgnoreCase) || a.Cause.Contains("load", StringComparison.OrdinalIgnoreCase)) + 24),
            new("Equipment",     6),
            new("Weather",       active.Count(a => a.Cause.Contains("weather", StringComparison.OrdinalIgnoreCase)) + 3),
        ];

        // Per-region 16-point latency series for the BigChart on the Insights page.
        // No time-series store yet — derive a smooth 16h trace ending at the region's
        // current load, so the curve always ends at "now" matching the ring health.
        // The endpoint stays cheap (no extra query) and the values reflect live state.
        IReadOnlyList<RegionLatencySeries> regionLatency = BuildRegionLatency(regionHealth);

        // Top copilot queries — group recent audit entries (Action == "copilot.query")
        // by Target text. Pulled from the actual audit log so this card "ticks" as users
        // ask things, instead of reading from a hardcoded list.
        IReadOnlyList<AuditEntry> recentQueries = await audit.ListByActionSinceAsync(
            "copilot.query", DateTime.UtcNow.AddHours(-24), 500, cancellationToken);
        IReadOnlyList<TopCopilotQuery> topQueries = recentQueries
            .GroupBy(e => Truncate(e.Target, 60), StringComparer.OrdinalIgnoreCase)
            .Select(g => new TopCopilotQuery(g.Key, g.Count()))
            .OrderByDescending(q => q.Count)
            .ThenBy(q => q.Query, StringComparer.Ordinal)
            .Take(5)
            .ToList();

        return Result.Success(new MetricsResponse(kpis, Sparks, regions, types, regionLatency, topQueries));
    }

    private static IReadOnlyList<RegionLatencySeries> BuildRegionLatency(IReadOnlyList<RegionHealth> regions)
    {
        // Pick the three "loudest" regions for the chart — the ones with the most signal
        // degradation from a healthy 95% baseline. Falls back to alphabetical if all healthy.
        IEnumerable<RegionHealth> ranked = regions
            .OrderBy(r => r.AvgSignalPct)
            .ThenBy(r => r.Region, StringComparer.Ordinal)
            .Take(3);

        string[] palette = ["var(--crit)", "var(--warn)", "var(--accent)"];
        var series = new List<RegionLatencySeries>();
        int idx = 0;
        foreach (RegionHealth r in ranked)
        {
            // Higher load → higher latency. Map signal% → an end-of-day latency in ms.
            int endLatency = Math.Clamp(160 - r.AvgSignalPct, 20, 160);
            int[] trace = new int[16];
            for (int i = 0; i < trace.Length; i++)
            {
                // Smooth ramp from a calm morning (~25ms) to the current end value, with
                // a tiny sinusoidal jitter so the curve isn't a perfect line.
                double t = i / (double)(trace.Length - 1);
                double baseline = 25 + (endLatency - 25) * t;
                double jitter = Math.Sin(i * 0.7 + r.Region.Length) * 3;
                trace[i] = (int)Math.Round(baseline + jitter);
            }
            series.Add(new RegionLatencySeries(r.Region, palette[idx % palette.Length], trace));
            idx++;
        }
        return series;
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max].TrimEnd() + "…";
}
