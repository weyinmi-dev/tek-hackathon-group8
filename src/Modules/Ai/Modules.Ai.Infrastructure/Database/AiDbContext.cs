using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Ai.Domain.Conversations;

namespace Modules.Ai.Infrastructure.Database;

public sealed class AiDbContext(DbContextOptions<AiDbContext> options) : DbContext(options)
{
    public DbSet<ChatLog> ChatLogs => Set<ChatLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema.Ai);

        modelBuilder.Entity<ChatLog>(b =>
        {
            b.ToTable("chat_logs");
            b.HasKey(c => c.Id);
            b.Property(c => c.Actor).HasMaxLength(64).IsRequired();
            b.Property(c => c.Question).HasMaxLength(2048).IsRequired();
            b.Property(c => c.Answer).HasMaxLength(8192).IsRequired();
            b.Property(c => c.SkillTrace).HasMaxLength(2048).IsRequired();
            b.HasIndex(c => c.OccurredAtUtc);
            b.Ignore(c => c.DomainEvents);
        });
    }
}
