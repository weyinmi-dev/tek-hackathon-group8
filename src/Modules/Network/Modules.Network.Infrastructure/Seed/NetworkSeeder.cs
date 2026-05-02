using Microsoft.EntityFrameworkCore;
using Modules.Network.Domain.Towers;
using Modules.Network.Infrastructure.Database;

namespace Modules.Network.Infrastructure.Seed;

public static class NetworkSeeder
{
    public static async Task SeedAsync(NetworkDbContext db, CancellationToken ct = default)
    {
#pragma warning disable IDE0011 // Add braces
        if (await db.Towers.AnyAsync(ct)) return;
#pragma warning restore IDE0011 // Add braces

        // Lagos metro tower mock dataset, mirrored from the design system.
        // (lat, lng) are real Lagos coords; (mapX, mapY) are 0–100 % positions
        // for the abstract NetworkMap render in the frontend.
        var seeds = new (string code, string name, string region, double lat, double lng, double x, double y, int sig, int load, TowerStatus status, string? issue)[]
        {
            ("TWR-LAG-W-014","Lagos West / Surulere","Lagos West",6.500,3.353,18,62,32,94,TowerStatus.Critical,"Backhaul fiber degradation"),
            ("TWR-LAG-W-022","Mushin Tower","Lagos West",6.530,3.355,24,55,58,81,TowerStatus.Warn,"Elevated packet loss"),
            ("TWR-LAG-W-031","Yaba North","Lagos West",6.510,3.378,32,60,64,74,TowerStatus.Warn,"Predicted failure 2h"),
            ("TWR-IKJ-007", "Ikeja GRA","Ikeja",6.605,3.349,30,30,88,54,TowerStatus.Ok,null),
            ("TWR-IKJ-019", "Ikeja Allen","Ikeja",6.598,3.358,36,34,62,78,TowerStatus.Warn,"Packet loss anomaly"),
            ("TWR-IKJ-021", "Maryland Mall","Ikeja",6.572,3.371,42,42,91,48,TowerStatus.Ok,null),
            ("TWR-LEK-003", "Lekki Phase 1","Lekki",6.448,3.475,74,74,18,12,TowerStatus.Critical,"60% packet loss — fiber cut"),
            ("TWR-LEK-008", "Lekki Phase 2","Lekki",6.439,3.523,82,78,55,88,TowerStatus.Warn,"Congestion overflow from LEK-003"),
            ("TWR-LEK-014", "Ajah Junction","Lekki",6.466,3.601,92,82,84,62,TowerStatus.Ok,null),
            ("TWR-VI-002",  "Victoria Island","Victoria Island",6.428,3.421,64,70,93,59,TowerStatus.Ok,null),
            ("TWR-VI-005",  "Eko Hotel","Victoria Island",6.421,3.428,66,74,90,66,TowerStatus.Ok,null),
            ("TWR-IKO-011", "Ikoyi South","Ikoyi",6.452,3.435,60,64,87,51,TowerStatus.Ok,null),
            ("TWR-APP-004", "Apapa Port","Apapa",6.450,3.365,28,74,82,71,TowerStatus.Ok,null),
            ("TWR-AGE-009", "Agege","Agege",6.625,3.319,14,22,86,45,TowerStatus.Ok,null),
            ("TWR-OJO-002", "Festac Town","Festac",6.469,3.290,8,68,60,79,TowerStatus.Warn,"Crowd-sourced reports +40%"),
        };

        var rnd = new Random(1337); // Fixed seed for reproducible demo

        foreach ((string code, string name, string region, double lat, double lng, double x, double y, int sig, int load, TowerStatus status, string? issue) s in seeds)
        {
            PowerSource activePower = (PowerSource)rnd.Next(0, 4); // Randomly 0-3
            double capacity = 1000.0;
            double fuel = rnd.NextDouble() * 800 + 100; // Between 100 and 900

            // If it's a specific tower we want to demo low fuel, let's force it
            if (s.code == "TWR-LAG-W-014")
            {
                activePower = PowerSource.Generator;
                fuel = 150.0;
            }

            await db.Towers.AddAsync(
                Tower.Create(s.code, s.name, s.region, s.lat, s.lng, s.x, s.y, s.sig, s.load, s.status, s.issue, activePower, fuel, capacity), ct);
        }
        await db.SaveChangesAsync(ct);
    }
}
