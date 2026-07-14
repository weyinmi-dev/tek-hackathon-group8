using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Analytics.Domain.Notifications;

namespace Modules.Analytics.Infrastructure.Database.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Kind).HasConversion<int>();
        builder.Property(n => n.Severity).HasConversion<int>();
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.SiteCode).HasMaxLength(32);
        builder.Property(n => n.Link).HasMaxLength(200);
        builder.Property(n => n.DedupeKey).HasMaxLength(128);

        // The feed query: unread first, newest first.
        builder.HasIndex(n => new { n.IsRead, n.RaisedAtUtc });

        // The dedupe check on every raise.
        builder.HasIndex(n => n.DedupeKey);

        builder.Ignore(n => n.DomainEvents);
    }
}
