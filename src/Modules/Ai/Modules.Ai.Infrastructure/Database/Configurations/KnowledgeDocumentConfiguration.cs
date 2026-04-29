using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Ai.Domain.Knowledge;

namespace Modules.Ai.Infrastructure.Database.Configurations;

internal sealed class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    public void Configure(EntityTypeBuilder<KnowledgeDocument> b)
    {
        b.ToTable("knowledge_documents");
        b.HasKey(d => d.Id);
        b.Property(d => d.SourceKey).HasMaxLength(128).IsRequired();
        b.HasIndex(d => d.SourceKey).IsUnique();
        b.Property(d => d.Category).HasConversion<int>().IsRequired();
        b.Property(d => d.Title).HasMaxLength(256).IsRequired();
        b.Property(d => d.Region).HasMaxLength(128).IsRequired();
        b.Property(d => d.Body).HasMaxLength(16_000).IsRequired();
        b.Property(d => d.Tags).HasMaxLength(512).IsRequired();
        b.Property(d => d.OccurredAtUtc).IsRequired();
        b.Property(d => d.IndexedAtUtc).IsRequired();
        b.HasIndex(d => d.Category);
        b.HasIndex(d => d.Region);
        b.Ignore(d => d.DomainEvents);
    }
}
