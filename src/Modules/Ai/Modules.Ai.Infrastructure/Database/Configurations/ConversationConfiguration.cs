using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Ai.Domain.Conversations;

namespace Modules.Ai.Infrastructure.Database.Configurations;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.ActorHandle).HasMaxLength(64).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(120).IsRequired();
        builder.Property(c => c.MessageCount).HasDefaultValue(0);

        // Sidebar listing query: WHERE user_id = X ORDER BY updated_at_utc DESC.
        builder.HasIndex(c => new { c.UserId, c.UpdatedAtUtc })
               .IsDescending(false, true);

        // Owns its messages — cascade delete keeps the per-message rows in sync
        // with the parent aggregate root.
        builder.HasMany(c => c.Messages)
               .WithOne()
               .HasForeignKey(m => m.ConversationId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(Conversation.Messages))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.DomainEvents);
    }
}
