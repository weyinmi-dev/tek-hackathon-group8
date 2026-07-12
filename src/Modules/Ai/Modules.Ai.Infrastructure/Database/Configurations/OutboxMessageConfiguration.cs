using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Ai.Infrastructure.Outbox;

namespace Modules.Ai.Infrastructure.Database.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_messages");
        b.HasKey(m => m.Id);
        b.Property(m => m.Type).HasMaxLength(512).IsRequired();
        b.Property(m => m.Payload).IsRequired();
        b.Property(m => m.OccurredAtUtc).IsRequired();
        b.Property(m => m.Error).HasMaxLength(4_000);

        // The processor polls "pending, oldest first"; index the processed flag + occurrence time.
        b.HasIndex(m => new { m.ProcessedAtUtc, m.OccurredAtUtc });
    }
}
