using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Network.Domain.Assets;
using Modules.Network.Domain.Maintenance;

namespace Modules.Network.Infrastructure.Database.Configurations;

internal sealed class SiteEquipmentConfiguration : IEntityTypeConfiguration<SiteEquipment>
{
    public void Configure(EntityTypeBuilder<SiteEquipment> builder)
    {
        builder.ToTable("site_equipment");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SiteCode).HasMaxLength(32).IsRequired();
        builder.Property(e => e.EquipmentId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Type).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Model).HasMaxLength(64);
        builder.Property(e => e.Status).HasMaxLength(32);

        // The idempotency key. A vendor equipment id is unique within a site, not across the fleet —
        // "BAT-001" exists at every site — so the constraint is on the pair. This is what makes
        // re-uploading the same snapshot update one row instead of inserting a duplicate.
        builder.HasIndex(e => new { e.SiteCode, e.EquipmentId }).IsUnique();

        builder.Ignore(e => e.DomainEvents);
    }
}

internal sealed class MaintenanceTicketConfiguration : IEntityTypeConfiguration<MaintenanceTicket>
{
    public void Configure(EntityTypeBuilder<MaintenanceTicket> builder)
    {
        builder.ToTable("maintenance_tickets");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.SiteCode).HasMaxLength(32).IsRequired();
        builder.Property(t => t.TicketId).HasMaxLength(64).IsRequired();
        builder.Property(t => t.Priority).HasMaxLength(32);
        builder.Property(t => t.ProviderStatus).HasMaxLength(32);
        builder.Property(t => t.Issue).HasMaxLength(512);
        builder.Property(t => t.CompletedAction).HasMaxLength(512);
        builder.Property(t => t.AssignedEngineerId).HasMaxLength(64);
        builder.Property(t => t.AssignedEngineerName).HasMaxLength(128);
        builder.Property(t => t.Status).HasConversion<int>();

        // Ticket ids are provider-global ("TT-20491"), so unlike equipment the key is the id alone.
        builder.HasIndex(t => t.TicketId).IsUnique();
        builder.HasIndex(t => new { t.SiteCode, t.Status });

        builder.Ignore(t => t.DomainEvents);
    }
}

internal sealed class EngineerConfiguration : IEntityTypeConfiguration<Engineer>
{
    public void Configure(EntityTypeBuilder<Engineer> builder)
    {
        builder.ToTable("engineers");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EngineerId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(128).IsRequired();

        builder.HasIndex(e => e.EngineerId).IsUnique();

        builder.Ignore(e => e.DomainEvents);
    }
}
