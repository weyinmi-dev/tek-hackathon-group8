using Microsoft.EntityFrameworkCore;
using Modules.Energy.Domain.Events;
using Modules.Energy.Domain.Sites;
using Modules.Energy.Domain.Telemetry;

namespace Modules.Energy.Infrastructure.Database;

public sealed class EnergyDbContext(DbContextOptions<EnergyDbContext> options) : DbContext(options)
{
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<BatteryHealth> Batteries => Set<BatteryHealth>();
    public DbSet<SiteEnergyLog> SiteLogs => Set<SiteEnergyLog>();
    public DbSet<FuelEvent> FuelEvents => Set<FuelEvent>();
    public DbSet<AnomalyEvent> Anomalies => Set<AnomalyEvent>();
    public DbSet<EnergyPrediction> Predictions => Set<EnergyPrediction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema.Energy);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EnergyDbContext).Assembly);
    }
}
