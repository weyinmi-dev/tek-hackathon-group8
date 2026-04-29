using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Ai.Application.Rag;
using Modules.Ai.Domain.Knowledge;

namespace Modules.Ai.Infrastructure.Database.Configurations;

/// <summary>
/// Maps <see cref="KnowledgeChunk"/> to a pgvector-backed table. The embedding
/// column type is <c>vector(N)</c> where N is the dimensionality of the
/// configured embedding model — pinned via the bound <see cref="RagOptions"/>.
/// </summary>
internal sealed class KnowledgeChunkConfiguration(RagOptions options) : IEntityTypeConfiguration<KnowledgeChunk>
{
    public void Configure(EntityTypeBuilder<KnowledgeChunk> b)
    {
        b.ToTable("knowledge_chunks");
        b.HasKey(c => c.Id);
        b.Property(c => c.DocumentId).IsRequired();
        b.Property(c => c.Ordinal).IsRequired();
        b.Property(c => c.Content).HasMaxLength(4_000).IsRequired();
        b.Property(c => c.TokenEstimate).IsRequired();

        // pgvector column. Dim is locked at index time — changing it later requires a re-index.
        string columnType = string.Create(CultureInfo.InvariantCulture, $"vector({options.EmbeddingDimensions})");
        b.Property(c => c.Embedding).HasColumnType(columnType).IsRequired();

        b.HasIndex(c => c.DocumentId);
        b.HasIndex(c => new { c.DocumentId, c.Ordinal }).IsUnique();
        b.Ignore(c => c.DomainEvents);
    }
}
