using Microsoft.EntityFrameworkCore;
using Modules.Alerts.Domain.Alerts;
using Modules.Alerts.Infrastructure.Database;

namespace Modules.Alerts.Infrastructure.Seed;

public static class AlertsSeeder
{
    public static async Task SeedAsync(AlertsDbContext db, CancellationToken ct = default)
    {
        if (await db.Alerts.AnyAsync(ct))
        {
            return;
        }


        DateTime now = DateTime.UtcNow;

        var seeds = new (string code, AlertSeverity sev, string title, string region, string tower, string cause, int users, double conf, AlertStatus status, int minutesAgo)[]
        {
            ("INC-2841", AlertSeverity.Critical, "60% packet loss — Lekki Phase 1",        "Lekki",      "TWR-LEK-003",     "Probable fiber cut on TG-LEK-A backhaul",                                          14200, 0.92, AlertStatus.Active,        2),
            ("INC-2840", AlertSeverity.Critical, "3 towers offline — power cluster",       "Lagos West", "TWR-LAG-W-014 +2", "Grid failure — IKEDC sector 7",                                                    38400, 0.88, AlertStatus.Active,        8),
            ("INC-2839", AlertSeverity.Warn,     "Predicted failure window",               "Lagos West", "TWR-LAG-W-031",   "Thermal trend + load → 87% probability of fault by 18:42",                            0, 0.87, AlertStatus.Active,        14),
            ("INC-2838", AlertSeverity.Warn,     "Crowd-sourced signal drop",              "Festac",     "TWR-OJO-002",     "42 user reports in 10min radius",                                                   1800, 0.71, AlertStatus.Investigating, 22),
            ("INC-2837", AlertSeverity.Info,     "Latency anomaly cleared",                "Ikeja",      "TWR-IKJ-019",     "Auto-resolved — load shed to TWR-IKJ-021",                                            0, 0.99, AlertStatus.Resolved,     47),
            ("INC-2836", AlertSeverity.Warn,     "Backhaul jitter elevated",               "Lagos West", "TWR-LAG-W-022",   "Microwave link MW-7 — weather correlated",                                          6200, 0.78, AlertStatus.Monitoring,   60),
        };

        foreach ((string code, AlertSeverity sev, string title, string region, string tower, string cause, int users, double conf, AlertStatus status, int minutesAgo) s in seeds)
        {
            await db.Alerts.AddAsync(
                Alert.Raise(s.code, s.sev, s.title, s.region, s.tower, s.cause, s.users, s.conf, s.status, now.AddMinutes(-s.minutesAgo)), ct);
        }
        await db.SaveChangesAsync(ct);
    }
}
