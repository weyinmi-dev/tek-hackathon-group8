using Microsoft.EntityFrameworkCore;
using Modules.Ai.Application.Rag;
using Modules.Ai.Domain.Conversations;
using Modules.Ai.Domain.Documents;
using Modules.Ai.Domain.Knowledge;
using Modules.Ai.Infrastructure.Database.Configurations;

namespace Modules.Ai.Infrastructure.Database;

public sealed class AiDbContext(DbContextOptions<AiDbContext> options, RagOptions ragOptions) : DbContext(options)
{
    private readonly RagOptions _ragOptions = ragOptions;

    public DbSet<ChatLog> ChatLogs => Set<ChatLog>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<ManagedDocument> ManagedDocuments => Set<ManagedDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema.Ai);
        modelBuilder.HasPostgresExtension("vector");

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

        modelBuilder.ApplyConfiguration(new KnowledgeDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new KnowledgeChunkConfiguration(_ragOptions));
        modelBuilder.ApplyConfiguration(new ManagedDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new ConversationConfiguration());
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
    }
}
