using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Ai.Domain.Conversations;

namespace Modules.Ai.Infrastructure.Database.Configurations;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Role).HasConversion<int>().IsRequired();
        // jsonb would be nicer but text keeps EnsureCreatedAsync portable across CI machines
        // without needing the snake_case converter to special-case it. Migrate to jsonb when
        // we move to real EF migrations.
        builder.Property(m => m.Content).HasColumnType("text").IsRequired();
        builder.Property(m => m.Metadata).HasColumnType("text");

        // Replay query for a conversation: WHERE conversation_id = X ORDER BY created_at_utc.
        builder.HasIndex(m => new { m.ConversationId, m.CreatedAtUtc });

        builder.Ignore(m => m.DomainEvents);
    }
}
