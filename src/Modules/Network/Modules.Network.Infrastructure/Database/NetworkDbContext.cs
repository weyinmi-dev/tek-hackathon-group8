using Microsoft.EntityFrameworkCore;
using Modules.Network.Domain.Ingestion;
using Modules.Network.Domain.Optimizations;
using Modules.Network.Domain.Towers;

namespace Modules.Network.Infrastructure.Database;

public sealed class NetworkDbContext(DbContextOptions<NetworkDbContext> options) : DbContext(options)
{
    public DbSet<Tower> Towers => Set<Tower>();
    public DbSet<IngestionRun> IngestionRuns => Set<IngestionRun>();
    public DbSet<NetworkEvent> NetworkEvents => Set<NetworkEvent>();
    public DbSet<Optimization> Optimizations => Set<Optimization>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema.Network);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NetworkDbContext).Assembly);
    }
}
