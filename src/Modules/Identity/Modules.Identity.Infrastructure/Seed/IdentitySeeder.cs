using Microsoft.EntityFrameworkCore;
using Modules.Identity.Application.Authentication;
using Modules.Identity.Domain.Users;
using Modules.Identity.Infrastructure.Database;
using SharedKernel;

namespace Modules.Identity.Infrastructure.Seed;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IdentityDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
#pragma warning disable IDE0011 // Add braces
        if (await db.Users.AnyAsync(ct)) return;
#pragma warning restore IDE0011 // Add braces

        // Default password for every demo user. Override per-user in real deployments.
#pragma warning disable S2068 // Credentials should not be hard-coded

        const string DemoPassword = "Telco!2025";
#pragma warning restore S2068 // Credentials should not be hard-coded

        string hash = hasher.Hash(DemoPassword);

        var seeds = new (string email, string name, string handle, string role, string team, string region)[]
        {
            ("oluwaseun.a@telco.lag", "Oluwaseun Adebayo", "oluwaseun.a", Roles.Engineer, "NOC Tier 2",    "Lagos Metro"),
            ("amaka.o@telco.lag",     "Amaka Okonkwo",     "amaka.o",     Roles.Manager,  "NOC Leadership","All regions"),
            ("tunde.b@telco.lag",     "Tunde Bakare",      "tunde.b",     Roles.Admin,    "Platform",      "All regions"),
            ("ifeanyi.k@telco.lag",   "Ifeanyi Kalu",      "ifeanyi.k",   Roles.Engineer, "NOC Tier 1",    "Lekki / VI"),
            ("halima.y@telco.lag",    "Halima Yusuf",      "halima.y",    Roles.Engineer, "Field Ops",     "Ikeja"),
            ("chioma.e@telco.lag",    "Chioma Eze",        "chioma.e",    Roles.Manager,  "Customer Ops",  "All regions"),
            ("baba.o@telco.lag",      "Babatunde Olu",     "baba.o",      Roles.Engineer, "NOC Tier 2",    "Lagos West"),
            ("kemi.a@telco.lag",      "Kemi Adekunle",     "kemi.a",      Roles.Viewer,   "Executive",     "All regions"),
        };

        foreach ((string email, string name, string handle, string role, string team, string region) in seeds)
        {
            Result<User> created = User.Create(email, hash, name, handle, role, team, region);
#pragma warning disable IDE0011 // Add braces
            if (created.IsSuccess) await db.Users.AddAsync(created.Value, ct);
#pragma warning restore IDE0011 // Add braces
        }
        await db.SaveChangesAsync(ct);
    }
}
