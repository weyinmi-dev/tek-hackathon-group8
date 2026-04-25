using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Analytics.Domain.Audit;

namespace Modules.Analytics.Infrastructure.Database.Configurations;

internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Actor).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Role).HasMaxLength(32).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Target).HasMaxLength(512).IsRequired();
        builder.Property(a => a.SourceIp).HasMaxLength(64).IsRequired();
        builder.HasIndex(a => a.OccurredAtUtc);
        builder.HasIndex(a => a.Actor);
        builder.Ignore(a => a.DomainEvents);
    }
}
