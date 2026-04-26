using Microsoft.EntityFrameworkCore;
using Modules.Analytics.Domain.Audit;
using Modules.Analytics.Infrastructure.Database;

namespace Modules.Analytics.Infrastructure.Seed;

public static class AnalyticsSeeder
{
    public static async Task SeedAsync(AnalyticsDbContext db, CancellationToken ct = default)
    {
#pragma warning disable IDE0011 // Add braces
        if (await db.AuditEntries.AnyAsync(ct)) return;
#pragma warning restore IDE0011 // Add braces

        DateTime now = DateTime.UtcNow;
        var seeds = new (int minutesAgo, string actor, string role, string action, string target, string ip)[]
        {
            (0,  "oluwaseun.a", "engineer", "copilot.query",  "Why is Lagos West slow?",                       "10.4.22.91"),
            (1,  "system",      "system",   "alert.raised",   "INC-2841 Lekki packet loss",                    "-"),
            (2,  "amaka.o",     "manager",  "incident.assign","INC-2840 → field-team-3",                       "10.4.22.14"),
            (4,  "oluwaseun.a", "engineer", "tower.diagnose", "TWR-LEK-003 deep probe",                        "10.4.22.91"),
            (7,  "system",      "system",   "sk.skill.run",   "NetworkDiagnostics.analyzeRegion(Lagos-West)",  "-"),
            (11, "tunde.b",     "admin",    "rbac.update",    "Granted engineer role → ifeanyi.k",             "10.4.22.5"),
            (14, "oluwaseun.a", "engineer", "copilot.query",  "Show outages last 2h on 4G",                    "10.4.22.91"),
            (18, "system",      "system",   "alert.predict",  "TWR-LAG-W-031 failure 87% by 18:42",            "-"),
            (24, "amaka.o",     "manager",  "report.export",  "Weekly NOC summary.pdf",                        "10.4.22.14"),
            (30, "tunde.b",     "admin",    "auth.login",     "OAuth2 / Azure AD",                             "10.4.22.5"),
        };

        foreach ((int minutesAgo, string actor, string role, string action, string target, string ip) s in seeds)
        {
            await db.AuditEntries.AddAsync(
                AuditEntry.RecordAt(now.AddMinutes(-s.minutesAgo), s.actor, s.role, s.action, s.target, s.ip), ct);
        }
        await db.SaveChangesAsync(ct);
    }
}
