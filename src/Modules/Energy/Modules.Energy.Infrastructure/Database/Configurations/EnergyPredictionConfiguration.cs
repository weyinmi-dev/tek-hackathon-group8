using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Energy.Domain.Events;

namespace Modules.Energy.Infrastructure.Database.Configurations;

internal sealed class EnergyPredictionConfiguration : IEntityTypeConfiguration<EnergyPrediction>
{
    public void Configure(EntityTypeBuilder<EnergyPrediction> builder)
    {
        builder.ToTable("predictions");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.SiteCode).HasMaxLength(32).IsRequired();
        builder.Property(p => p.Kind).HasConversion<int>();
        builder.Property(p => p.Detail).HasMaxLength(256).IsRequired();
        builder.HasIndex(p => p.SiteCode);
        builder.HasIndex(p => p.WindowEndsUtc);
        builder.Ignore(p => p.DomainEvents);
    }
}
