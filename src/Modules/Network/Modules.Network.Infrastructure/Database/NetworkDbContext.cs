using Microsoft.EntityFrameworkCore;
using Modules.Network.Domain.Towers;

namespace Modules.Network.Infrastructure.Database;

public sealed class NetworkDbContext(DbContextOptions<NetworkDbContext> options) : DbContext(options)
{
    public DbSet<Tower> Towers => Set<Tower>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema.Network);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NetworkDbContext).Assembly);
    }
}
