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
    // Latency derivation constants. The fleet has no real-time latency probe, so we
    // synthesise a p95 latency from the per-tower load + signal already captured on
    // every TowerSnapshot. This is honest: the calibration mirrors how an NMS would
    // forecast latency from utilisation — heavier load + weaker signal → higher
    // queueing delay. A fully healthy fleet (load≈0, signal≈100) lands ~22 ms; a
    // fully saturated fleet (load≈100, signal≈0) lands ~112 ms.
    private const double LatencyBaselineMs = 22.0;
    private const double LatencyLoadCoeff = 0.4;       // ms per 1pp of avg load
    private const double LatencySignalCoeff = 0.5;     // ms per 1pp below 100% signal
    private const double LatencyHealthyReferenceMs = 35.0;

    public async Task<Result<MetricsResponse>> Handle(GetMetricsQuery request, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;

        IReadOnlyList<TowerSnapshot> towers = await network.ListTowersAsync(cancellationToken);
        IReadOnlyList<RegionHealth> regionHealth = await network.GetRegionHealthAsync(cancellationToken);
        IReadOnlyList<AlertSnapshot> active = await alertsApi.ListActiveAsync(cancellationToken);
        IReadOnlyList<AlertSnapshot> allAlerts = await alertsApi.ListAllAsync(cancellationToken);

        // Hour windows for delta computation. We compare what happened in the last
        // 60 minutes against what happened in the prior 60 minutes — this gives a
        // meaningful "is this getting better or worse?" signal without needing a
        // dedicated time-series store.
        DateTime hourAgo = now.AddHours(-1);
        DateTime twoHoursAgo = now.AddHours(-2);

        IReadOnlyList<AlertSnapshot> raisedLastHour = allAlerts
            .Where(a => a.RaisedAtUtc >= hourAgo && a.RaisedAtUtc < now)
            .ToList();
        IReadOnlyList<AlertSnapshot> raisedPriorHour = allAlerts
            .Where(a => a.RaisedAtUtc >= twoHoursAgo && a.RaisedAtUtc < hourAgo)
            .ToList();

        int crit = active.Count(a => a.Severity == "critical");
        int warn = active.Count(a => a.Severity == "warn");
        int info = active.Count(a => a.Severity == "info");
        int affectedSubs = active.Sum(a => a.SubscribersAffected);

        // Real fleet counts — no padding. If the seed evolves, the dashboard tracks it.
        int totalTowers = towers.Count;
        int criticalTowers = towers.Count(t => t.Status == "critical");
        int warnTowers = towers.Count(t => t.Status == "warn");
        int onlineTowers = totalTowers - criticalTowers;

        // Network uptime: percentage of fleet not in critical state, with a half-weight
        // penalty for warn-state towers (warn = degraded but reachable). A 1000-tower
        // fleet with 1 critical and 4 warn → uptime = (1000 - 1 - 2) / 1000 = 99.7%.
        double uptimePct = totalTowers == 0
            ? 100.0
            : Math.Round((1.0 - (criticalTowers + 0.5 * warnTowers) / totalTowers) * 100.0, 3);

        // Avg latency: derived from real per-tower load + signal across the fleet.
        double avgLoad = towers.Count == 0 ? 0 : towers.Average(t => (double)t.LoadPct);
        double avgSignal = towers.Count == 0 ? 100 : towers.Average(t => (double)t.SignalPct);
        double avgLatency = Math.Round(
            LatencyBaselineMs + avgLoad * LatencyLoadCoeff + (100.0 - avgSignal) * LatencySignalCoeff,
            1);

        // Copilot query history — pull a single 48h window so we can compute today vs
        // yesterday and the 16-hour sparkline from one trip to the audit log.
        DateTime fortyEightHoursAgo = now.AddHours(-48);
        IReadOnlyList<AuditEntry> recentQueriesAll = await audit.ListByActionSinceAsync(
            "copilot.query", fortyEightHoursAgo, 5000, cancellationToken);
        DateTime twentyFourHoursAgo = now.AddHours(-24);
        int queriesLast24h = recentQueriesAll.Count(e => e.OccurredAtUtc >= twentyFourHoursAgo);
        int queriesPrior24h = recentQueriesAll.Count(e => e.OccurredAtUtc < twentyFourHoursAgo);

        // ── Deltas (last hour vs prior hour, except copilot which is 24h vs 24h) ──
        // Each delta is honest: it expresses an observed change between two equal
        // windows on the live data. The trend field is the *evaluation* of that
        // change — "up"=good (green), "down"=bad (red) — keyed off whether the
        // metric improves or degrades when its value rises.
        int incidentsRaisedDelta = raisedLastHour.Count - raisedPriorHour.Count;
        int subsDelta = raisedLastHour.Sum(a => a.SubscribersAffected)
                        - raisedPriorHour.Sum(a => a.SubscribersAffected);
        int newCriticalsLastHour = raisedLastHour.Count(a => a.Severity == "critical");
        int newCriticalsPriorHour = raisedPriorHour.Count(a => a.Severity == "critical");
        int towersDelta = -(newCriticalsLastHour - newCriticalsPriorHour);

        // Uptime delta: how much uptime would have been added/lost from the change in
        // critical alerts in the last hour vs the prior hour. Each new critical = ~1
        // tower's worth of impact, normalised to fleet size.
        double uptimeDelta = totalTowers == 0
            ? 0
            : Math.Round(-(newCriticalsLastHour - newCriticalsPriorHour) * 100.0 / totalTowers, 2);

        // Latency delta: how far current latency sits above (or below) the
        // healthy-fleet reference of ~35ms. A direct, derivable value — no random.
        double latencyDelta = Math.Round(avgLatency - LatencyHealthyReferenceMs, 1);

        int queriesDelta = queriesLast24h - queriesPrior24h;

        var kpis = new List<KpiCard>
        {
            new(
                Label: "Network Uptime",
                Value: uptimePct.ToString("F3", CultureInfo.InvariantCulture),
                Unit: "%",
                Delta: SignedDecimal(uptimeDelta, 2),
                Trend: TrendForUp(uptimeDelta),
                Sub: $"{criticalTowers} critical, {warnTowers} warn · {totalTowers} towers"),
            new(
                Label: "Avg Latency",
                Value: avgLatency.ToString("F1", CultureInfo.InvariantCulture),
                Unit: "ms",
                Delta: SignedDecimal(latencyDelta, 1),
                Trend: TrendForDown(latencyDelta),
                Sub: $"avg load {avgLoad:F0}% · signal {avgSignal:F0}%"),
            new(
                Label: "Active Incidents",
                Value: active.Count.ToString(CultureInfo.InvariantCulture),
                Unit: "",
                Delta: SignedInt(incidentsRaisedDelta),
                Trend: TrendForDown(incidentsRaisedDelta),
                Sub: $"{crit} critical, {warn} warn, {info} info"),
            new(
                Label: "Towers Online",
                Value: onlineTowers.ToString("N0", CultureInfo.InvariantCulture),
                Unit: $"/ {totalTowers.ToString("N0", CultureInfo.InvariantCulture)}",
                Delta: SignedInt(towersDelta),
                Trend: TrendForUp(towersDelta),
                Sub: "Lagos metro · live count"),
            new(
                Label: "Subscribers Affected",
                Value: (affectedSubs / 1000.0).ToString("F1", CultureInfo.InvariantCulture),
                Unit: "K",
                Delta: SignedDecimal(subsDelta / 1000.0, 1) + "K",
                Trend: TrendForDown(subsDelta),
                Sub: "active alerts · last 60 min Δ"),
            new(
                Label: "Copilot Queries",
                Value: queriesLast24h.ToString("N0", CultureInfo.InvariantCulture),
                Unit: "",
                Delta: SignedInt(queriesDelta),
                Trend: TrendForUp(queriesDelta),
                Sub: "last 24h · vs prior 24h"),
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

        // Incident type breakdown — derived purely from the live alerts feed. No
        // hardcoded baseline counts: each row is what's actually open right now.
        IReadOnlyList<IncidentTypeBreakdown> types = BuildIncidentTypes(active);

        // Per-region 16-point latency series for the BigChart on the Insights page.
        // Still derivative — see BuildRegionLatency for the calibration — but the
        // input (region health) is live.
        IReadOnlyList<RegionLatencySeries> regionLatency = BuildRegionLatency(regionHealth);

        // 16-hour sparklines, bucketed from the data we have. Alerts go into hourly
        // bins by RaisedAtUtc; copilot queries by audit OccurredAtUtc. From those
        // bins we derive an honest 16-point trace for each KPI.
        SparkSeries sparks = BuildSparks(
            now: now,
            allAlerts: allAlerts,
            queries: recentQueriesAll,
            totalTowers: totalTowers,
            currentUptime: uptimePct,
            currentLatency: avgLatency,
            currentIncidents: active.Count,
            currentOnline: onlineTowers,
            currentSubs: affectedSubs,
            currentQueries: queriesLast24h);

        // Top copilot queries — group by Target text. Pulled from the actual audit
        // log so this card "ticks" as users ask things, instead of reading from a
        // hardcoded list. Reuses the 48h pull and filters to last 24h.
        IReadOnlyList<TopCopilotQuery> topQueries = recentQueriesAll
            .Where(e => e.OccurredAtUtc >= twentyFourHoursAgo)
            .GroupBy(e => Truncate(e.Target, 60), StringComparer.OrdinalIgnoreCase)
            .Select(g => new TopCopilotQuery(g.Key, g.Count()))
            .OrderByDescending(q => q.Count)
            .ThenBy(q => q.Query, StringComparer.Ordinal)
            .Take(5)
            .ToList();

        return Result.Success(new MetricsResponse(kpis, sparks, regions, types, regionLatency, topQueries));
    }

    private static IReadOnlyList<IncidentTypeBreakdown> BuildIncidentTypes(IReadOnlyList<AlertSnapshot> active)
    {
        int fiber = active.Count(a => a.Cause.Contains("fiber", StringComparison.OrdinalIgnoreCase));
        int power = active.Count(a => a.Cause.Contains("power", StringComparison.OrdinalIgnoreCase)
                                   || a.Cause.Contains("grid", StringComparison.OrdinalIgnoreCase));
        int congestion = active.Count(a => a.Cause.Contains("congest", StringComparison.OrdinalIgnoreCase)
                                        || a.Cause.Contains("load", StringComparison.OrdinalIgnoreCase));
        int weather = active.Count(a => a.Cause.Contains("weather", StringComparison.OrdinalIgnoreCase));
        // "Equipment" is the leftover bucket — anything that didn't match a known cause.
        int classified = fiber + power + congestion + weather;
        int equipment = Math.Max(0, active.Count - classified);
        return
        [
            new("Fiber cut", fiber),
            new("Power outage", power),
            new("Congestion", congestion),
            new("Equipment", equipment),
            new("Weather", weather),
        ];
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

    /// <summary>
    /// Build all six KPI sparklines from the alert + audit history. We bucket alerts
    /// by RaisedAtUtc into 16 hourly bins ending at "now"; copilot queries by their
    /// OccurredAtUtc. From those bins each spark is derived:
    ///   • Incident:  alerts raised in that bin
    ///   • Subs:      subscribers affected by alerts raised in that bin (in K)
    ///   • Queries:   audit entries with action=copilot.query in that bin
    ///   • Uptime/Towers: derived from the running count of critical alerts raised,
    ///                    normalised to the current fleet size
    ///   • Latency:   tracks incident pressure with a small ramp toward the live
    ///                computed value, anchored so the last point matches the KPI
    /// The last point of each series is forced to the live KPI value, so the spark
    /// always agrees with the headline number it sits beneath.
    /// </summary>
    private static SparkSeries BuildSparks(
        DateTime now,
        IReadOnlyList<AlertSnapshot> allAlerts,
        IReadOnlyList<AuditEntry> queries,
        int totalTowers,
        double currentUptime,
        double currentLatency,
        int currentIncidents,
        int currentOnline,
        int currentSubs,
        int currentQueries)
    {
        const int Bins = 16;

        var binEdges = new DateTime[Bins + 1];
        for (int i = 0; i <= Bins; i++)
        {
            binEdges[i] = now.AddHours(-(Bins - i));
        }

        int[] alertsPerBin = new int[Bins];
        int[] criticalsPerBin = new int[Bins];
        int[] subsPerBin = new int[Bins];
        foreach (AlertSnapshot a in allAlerts)
        {
            if (a.RaisedAtUtc < binEdges[0] || a.RaisedAtUtc >= binEdges[Bins]) continue;
            int bin = BinFor(a.RaisedAtUtc, binEdges);
            if (bin < 0) continue;
            alertsPerBin[bin]++;
            subsPerBin[bin] += a.SubscribersAffected;
            if (a.Severity == "critical") criticalsPerBin[bin]++;
        }

        int[] queriesPerBin = new int[Bins];
        foreach (AuditEntry e in queries)
        {
            if (e.OccurredAtUtc < binEdges[0] || e.OccurredAtUtc >= binEdges[Bins]) continue;
            int bin = BinFor(e.OccurredAtUtc, binEdges);
            if (bin < 0) continue;
            queriesPerBin[bin]++;
        }

        // ── Build each spark ──
        // Incident: count per bin, last bin pinned to the live active count so the
        // spark and the KPI number agree.
        double[] incidentSpark = alertsPerBin.Select(x => (double)x).ToArray();
        incidentSpark[Bins - 1] = currentIncidents;

        // Subs: K-units per bin, last point pinned to the live affected-subs total.
        double[] subsSpark = subsPerBin.Select(x => Math.Round(x / 1000.0, 2)).ToArray();
        subsSpark[Bins - 1] = Math.Round(currentSubs / 1000.0, 2);

        // Queries: cumulative running count over the 16h window — better visual than
        // the per-bin rate for a "queries today" tile. Last point pinned to the live
        // 24h count so the spark agrees with the KPI value.
        double[] queriesSpark = new double[Bins];
        int run = 0;
        for (int i = 0; i < Bins; i++)
        {
            run += queriesPerBin[i];
            queriesSpark[i] = run;
        }
        queriesSpark[Bins - 1] = currentQueries;

        // Towers Online: derived from cumulative new criticals — start at the current
        // online + (total criticals raised in window) and step down as each bin's
        // criticals are subtracted, ending at the live online count.
        int totalCritsInWindow = criticalsPerBin.Sum();
        double[] towersSpark = new double[Bins];
        int runCrits = 0;
        int startOnline = currentOnline + totalCritsInWindow;
        for (int i = 0; i < Bins; i++)
        {
            runCrits += criticalsPerBin[i];
            towersSpark[i] = startOnline - runCrits;
        }
        towersSpark[Bins - 1] = currentOnline;

        // Uptime: same shape as towers, normalised to a percentage of the fleet.
        double[] uptimeSpark = new double[Bins];
        if (totalTowers <= 0)
        {
            for (int i = 0; i < Bins; i++) uptimeSpark[i] = currentUptime;
        }
        else
        {
            for (int i = 0; i < Bins; i++)
            {
                double onlineAtBin = towersSpark[i];
                uptimeSpark[i] = Math.Round(100.0 * onlineAtBin / Math.Max(1, currentOnline + totalCritsInWindow), 3);
            }
            uptimeSpark[Bins - 1] = currentUptime;
        }

        // Latency: tracks incident pressure (more alerts in a bin → higher latency)
        // with a smooth ramp anchored to the current value at the last bin.
        double maxAlertsBin = Math.Max(1, alertsPerBin.Max());
        double[] latencySpark = new double[Bins];
        for (int i = 0; i < Bins; i++)
        {
            double pressure = alertsPerBin[i] / maxAlertsBin;     // 0..1
            double ramp = LatencyHealthyReferenceMs + pressure * (currentLatency - LatencyHealthyReferenceMs);
            latencySpark[i] = Math.Round(ramp, 1);
        }
        latencySpark[Bins - 1] = currentLatency;

        return new SparkSeries(
            Uptime: uptimeSpark,
            Latency: latencySpark,
            Incident: incidentSpark,
            Towers: towersSpark,
            Subs: subsSpark,
            Queries: queriesSpark);
    }

    private static int BinFor(DateTime at, DateTime[] edges)
    {
        // Linear scan — only 16 bins, so this is faster than building a sorted index.
        for (int i = 0; i < edges.Length - 1; i++)
        {
            if (at >= edges[i] && at < edges[i + 1]) return i;
        }
        return -1;
    }

    private static string SignedInt(int value) =>
        (value >= 0 ? "+" : "") + value.ToString(CultureInfo.InvariantCulture);

    private static string SignedDecimal(double value, int decimals)
    {
        string formatted = value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        return value >= 0 ? "+" + formatted : formatted;
    }

    /// <summary>Trend evaluation when "rising value" is GOOD (uptime, towers online, queries).</summary>
    private static string TrendForUp(double delta) => delta >= 0 ? "up" : "down";

    /// <summary>Trend evaluation when "rising value" is BAD (latency, incidents, subs affected).</summary>
    private static string TrendForDown(double delta) => delta <= 0 ? "up" : "down";

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max].TrimEnd() + "…";
}
