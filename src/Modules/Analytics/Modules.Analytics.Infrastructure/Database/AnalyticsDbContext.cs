using Microsoft.EntityFrameworkCore;
using Modules.Analytics.Domain.Audit;

namespace Modules.Analytics.Infrastructure.Database;

public sealed class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : DbContext(options)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema.Analytics);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AnalyticsDbContext).Assembly);
    }
}
