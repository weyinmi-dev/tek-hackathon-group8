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

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<ManagedDocument> ManagedDocuments => Set<ManagedDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema.Ai);
        modelBuilder.HasPostgresExtension("vector");

        // ChatLog is gone (Phase 3 M14): a flat audit mirror of the conversation, written by the old
        // copilot handler and read by nothing. Conversations + Messages are the source of truth.
        // The existing ai.chat_logs table is simply left unmapped — drop it whenever convenient.
        modelBuilder.ApplyConfiguration(new KnowledgeDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new KnowledgeChunkConfiguration(_ragOptions));
        modelBuilder.ApplyConfiguration(new ManagedDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new ConversationConfiguration());
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new WorkflowCheckpointConfiguration());
    }
}
