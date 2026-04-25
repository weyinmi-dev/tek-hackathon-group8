using Microsoft.EntityFrameworkCore;
using Modules.Alerts.Domain.Alerts;

namespace Modules.Alerts.Infrastructure.Database;

public sealed class AlertsDbContext(DbContextOptions<AlertsDbContext> options) : DbContext(options)
{
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema.Alerts);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AlertsDbContext).Assembly);
    }
}
