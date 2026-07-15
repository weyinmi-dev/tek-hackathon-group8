using Microsoft.EntityFrameworkCore;
using Modules.Network.Domain.Assets;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Domain.Maintenance;
using Modules.Network.Domain.Optimizations;
using Modules.Network.Domain.Towers;

namespace Modules.Network.Infrastructure.Database;

public sealed class NetworkDbContext(DbContextOptions<NetworkDbContext> options) : DbContext(options)
{
    public DbSet<Tower> Towers => Set<Tower>();
    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();
    public DbSet<NetworkEvent> NetworkEvents => Set<NetworkEvent>();
    public DbSet<SiteSnapshotRecord> SiteSnapshots => Set<SiteSnapshotRecord>();
    public DbSet<Optimization> Optimizations => Set<Optimization>();

    // Site assets and field maintenance. These hang off Tower.Code — the same key the snapshot
    // joins on — so they live in the Network module rather than a module of their own.
    public DbSet<SiteEquipment> SiteEquipment => Set<SiteEquipment>();
    public DbSet<MaintenanceTicket> MaintenanceTickets => Set<MaintenanceTicket>();
    public DbSet<Engineer> Engineers => Set<Engineer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema.Network);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NetworkDbContext).Assembly);
    }
}
