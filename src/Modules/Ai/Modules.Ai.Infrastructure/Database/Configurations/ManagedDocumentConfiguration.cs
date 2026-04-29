using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Ai.Domain.Documents;

namespace Modules.Ai.Infrastructure.Database.Configurations;

internal sealed class ManagedDocumentConfiguration : IEntityTypeConfiguration<ManagedDocument>
{
    public void Configure(EntityTypeBuilder<ManagedDocument> builder)
    {
        builder.ToTable("managed_documents");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Title).HasMaxLength(256).IsRequired();
        builder.Property(d => d.FileName).HasMaxLength(256).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(d => d.Region).HasMaxLength(64).IsRequired();
        builder.Property(d => d.Tags).HasMaxLength(512).IsRequired();
        builder.Property(d => d.StorageKey).HasMaxLength(512).IsRequired();
        builder.Property(d => d.ExternalReference).HasMaxLength(1024);
        builder.Property(d => d.UploadedBy).HasMaxLength(64).IsRequired();
        builder.Property(d => d.LastIndexError).HasMaxLength(2048);

        builder.Property(d => d.Source).HasConversion<int>();
        builder.Property(d => d.Status).HasConversion<int>();
        builder.Property(d => d.Category).HasConversion<int>();

        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => new { d.Source, d.StorageKey });

        builder.Ignore(d => d.DomainEvents);
    }
}
