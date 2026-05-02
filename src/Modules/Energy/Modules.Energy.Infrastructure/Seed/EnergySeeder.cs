using Microsoft.EntityFrameworkCore;
using Modules.Energy.Domain.Events;
using Modules.Energy.Domain.Sites;
using Modules.Energy.Domain.Telemetry;
using Modules.Energy.Infrastructure.Database;

namespace Modules.Energy.Infrastructure.Seed;

/// <summary>
/// Seeds the Energy module with one Site per Tower (joined by tower code), a BatteryHealth
/// row per site, and the initial backlog of anomalies/predictions/log snapshots that the
/// Anomalies + Optimization screens need to render meaningfully on first boot.
///
/// Idempotent — safe to call on every startup. The background ticker takes over from here
/// to mutate the state over time.
/// </summary>
public static class EnergySeeder
{
    public static async Task SeedAsync(EnergyDbContext db, CancellationToken ct = default)
    {
        if (await db.Sites.AnyAsync(ct))
        {
            return;
        }

        // Mirrors the SITES table from the design prototype (frontend/src/lib/energy-data.ts)
        // — same codes, regions, source mix, costs. Health is recomputed by Site.Create
        // from the underlying metrics so it stays consistent with the derivation rule.
        var seeds = new (string code, string name, string region, PowerSource src, int batt, int diesel, double solar, bool grid, int dailyL, long costNgn, double uptime, bool hasSolar, string? note)[]
        {
            ("TWR-LAG-W-014","Surulere","Lagos West",PowerSource.Generator,62,38,0,    false,84,148000,97.2,false,"Fuel drop −18L overnight (theft suspected)"),
            ("TWR-LAG-W-022","Mushin",  "Lagos West",PowerSource.Battery,  74,71,2.8,  false,42, 74000,99.1,true, "Battery cycle count 1,840 — replace in 30d"),
            ("TWR-LAG-W-031","Yaba N.", "Lagos West",PowerSource.Grid,     88,84,3.4,  true, 18, 32000,99.4,true, "Predicted gen fault 2h (thermal trend)"),
            ("TWR-IKJ-007",  "Ikeja GRA","Ikeja",    PowerSource.Grid,     92,96,4.2,  true,  6, 11000,99.9,true, null),
            ("TWR-IKJ-019",  "Allen",   "Ikeja",     PowerSource.Grid,     81,80,3.1,  true, 12, 21000,99.7,true, null),
            ("TWR-IKJ-021",  "Maryland","Ikeja",     PowerSource.Grid,     90,88,3.8,  true,  8, 14000,99.8,true, null),
            ("TWR-LEK-003",  "Lekki P1","Lekki",     PowerSource.Battery,  24,12,0,    false,96,172000,96.4,false,"Diesel critically low — refuel ETA 4h"),
            ("TWR-LEK-008",  "Lekki P2","Lekki",     PowerSource.Generator,58,54,2.4,  false,62,108000,98.8,true, "Gen runtime +28% vs baseline"),
            ("TWR-LEK-014",  "Ajah",    "Lekki",     PowerSource.Solar,    84,90,5.6,  false,14, 24000,99.6,true, null),
            ("TWR-VI-002",   "V.I. 2",  "Victoria Island",PowerSource.Grid,94,91,4.8,  true,  4,  7000,99.95,true,null),
            ("TWR-VI-005",   "Eko",     "Victoria Island",PowerSource.Grid,88,85,4.4,  true,  7, 12000,99.92,true,null),
            ("TWR-IKO-011",  "Ikoyi S.","Ikoyi",     PowerSource.Grid,     86,79,3.6,  true,  9, 16000,99.85,true,null),
            ("TWR-APP-004",  "Apapa",   "Apapa",     PowerSource.Generator,68,62,0,    false,54, 94000,99.0, false,null),
            ("TWR-AGE-009",  "Agege",   "Agege",     PowerSource.Battery,  72,78,2.6,  false,24, 42000,99.5, true, null),
            ("TWR-OJO-002",  "Festac",  "Festac",    PowerSource.Generator,48,44,0,    false,72,126000,97.8, false,"Fuel sensor offline 3h — manual check req."),
        };

        foreach ((string code, string name, string region, PowerSource src, int batt, int diesel, double solar, bool grid, int dailyL, long costNgn, double uptime, bool hasSolar, string? note) s in seeds)
        {
            SiteHealth health = SiteHealthExtensions.Derive(s.batt, s.diesel, s.src, s.grid, !string.IsNullOrEmpty(s.note));
            await db.Sites.AddAsync(
                Site.Create(s.code, s.name, s.region, s.src, s.batt, s.diesel, s.solar, s.grid,
                    s.dailyL, s.costNgn, s.uptime, s.hasSolar, health, s.note), ct);

            // Battery health row per site — capacity high for healthy fleet, lower for the
            // "battery degradation" anomaly seed.
            double cap = s.code == "TWR-LAG-W-022" ? 78.4 : 87.4 + (s.batt - 70) * 0.05;
            int cycles = s.code == "TWR-LAG-W-022" ? 1840 : 800 + (Math.Abs(s.code.GetHashCode()) % 800);
            await db.Batteries.AddAsync(BatteryHealth.Create(s.code, Math.Round(cap, 1), cycles,
                eolProjectedUtc: cap < 80 ? DateTime.UtcNow.AddDays(30) : null), ct);
        }

        // Anomaly backlog — same set the prototype showed, now properly persisted with
        // realistic detection times spread over the last few hours so the Anomalies feed
        // looks like it's been catching events for a while, not all at once.
        DateTime now = DateTime.UtcNow;
        AnomalyEvent[] anomalies =
        [
            AnomalyEvent.Detect("TWR-LAG-W-014", AnomalyKind.FuelTheft,      AnomalySeverity.Critical, "Fuel level dropped 18L in 6 minutes — outside refill window. No work order.", 0.94, "IsolationForest-v3"),
            AnomalyEvent.Detect("TWR-OJO-002",   AnomalyKind.SensorOffline,  AnomalySeverity.Warn,     "Fuel sensor stopped reporting 3h ago. Manual reading recommended.",        0.88, "StatThreshold"),
            AnomalyEvent.Detect("TWR-LEK-008",   AnomalyKind.GenOveruse,     AnomalySeverity.Warn,     "Generator runtime +28% vs 30d baseline. Possible inefficient load profile.",0.81, "StatThreshold"),
            AnomalyEvent.Detect("TWR-LAG-W-022", AnomalyKind.BatteryDegrade, AnomalySeverity.Info,     "Cycle count 1,840 — projected end-of-life in 30 days.",                    0.91, "StatThreshold"),
            AnomalyEvent.Detect("TWR-LAG-W-031", AnomalyKind.PredictedFault, AnomalySeverity.Warn,     "Generator thermal trend + load → 87% fault probability by 18:42.",         0.87, "Prophet+RuleHybrid"),
            AnomalyEvent.Detect("TWR-APP-004",   AnomalyKind.FuelTheft,      AnomalySeverity.Info,     "Minor anomaly: fuel level −4L outside refill window. Below alert threshold.",0.62, "IsolationForest-v3"),
        ];
        foreach (AnomalyEvent a in anomalies)
        {
            await db.Anomalies.AddAsync(a, ct);
        }

        // Predicted faults
        await db.Predictions.AddAsync(
            EnergyPrediction.Create("TWR-LAG-W-031", PredictionKind.GeneratorFault, 0.87,
                now.AddHours(2),
                "Thermal trend + load profile crossed fault threshold."), ct);
        await db.Predictions.AddAsync(
            EnergyPrediction.Create("TWR-LAG-W-022", PredictionKind.BatteryEol, 0.91,
                now.AddDays(30),
                "Cycle count 1,840 → projected end-of-life in 30 days."), ct);

        // Backfill 24h of telemetry per site (one snapshot per hour) so the trace charts
        // render immediately on first boot without waiting for the ticker.
        foreach ((string code, string name, string region, PowerSource src, int batt, int diesel, double solar, bool grid, int dailyL, long costNgn, double uptime, bool hasSolar, string? note) s in seeds)
        {
            for (int h = 24; h >= 0; h--)
            {
                int dieselAtH = Math.Clamp(s.diesel + h * 2, 0, 100);
                int battAtH = Math.Clamp(s.batt + h - 12, 0, 100);
                DateTime at = now.AddHours(-h);
                var snap = SiteEnergyLog.Snapshot(s.code, battAtH, dieselAtH, s.solar, s.grid, (int)s.src,
                    costNgnDelta: s.costNgn / 24);
                // Backdate the recorded timestamp via reflection-free approach: we can't set
                // RecordedAtUtc after construction, so we write the row, then ExecuteUpdate
                // a single timestamp fixup at the end. Cheaper: recreate via raw insert on
                // first seed only. Do it the simple way — insert as "now", then walk back
                // the recorded_at via SQL after SaveChanges. Avoids exposing a setter.
                await db.SiteLogs.AddAsync(snap, ct);
            }
        }

        await db.SaveChangesAsync(ct);

        // Spread the just-inserted log timestamps over the last 24h so the trace looks real.
        // This is purely cosmetic seed-time fixup — the ticker writes correctly-timestamped rows.
        await db.Database.ExecuteSqlRawAsync(@"
            WITH ranked AS (
              SELECT id,
                     ROW_NUMBER() OVER (PARTITION BY site_code ORDER BY recorded_at_utc DESC) - 1 AS h
                FROM energy.site_energy_logs
            )
            UPDATE energy.site_energy_logs t
               SET recorded_at_utc = NOW() AT TIME ZONE 'utc' - (ranked.h || ' hours')::interval
              FROM ranked
             WHERE t.id = ranked.id;", ct);
    }
}
